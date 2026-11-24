using Brasileirao;

// Série D: 16 groups of six, ten rounds, a 64-club two-legged knockout, six promoted clubs.
public static class SerieDTests
{
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }

    public static void Run()
    {
        // Directory and groups.
        var versions = SerieD.Versions().ToList();
        var catalogs = Enumerable.Range(1, SerieD.GroupCount).Select(SerieD.Catalog).ToList();
        Check(SerieD.GroupCount == 16 && versions.Count == 16 && versions.Distinct().Count() == 16, "Série D tem 16 grupos, cada um com sua versão de dados");
        Check(catalogs.All(c => c.Clubs.Count == 6) && catalogs.SelectMany(c => c.Clubs).Select(c => c.Id).Distinct().Count() == 96, "Os 96 clubes estão em grupos de seis, sem repetição");
        catalogs.ForEach(CatalogImport.Validate);
        Check(true, "Catálogo de cada grupo passa na validação");
        Check(catalogs.All(c => SerieD.IsVersion(c.Version) && SerieD.GroupOfVersion(c.Version) == catalogs.IndexOf(c) + 1) && !SerieD.IsVersion(Game.SerieC2026Version), "Versão de dados identifica o grupo");
        var directory = System.Text.Json.Nodes.JsonNode.Parse(Game.DirectoryJson())!.AsObject();
        var dClubs = directory["clubs"]!.AsArray().OfType<System.Text.Json.Nodes.JsonObject>().Where(c => c["series"]!.GetValue<string>() == "D").ToList();
        Check(dClubs.Count == 96 && dClubs.All(c => SerieD.IsVersion(c["version"]!.GetValue<string>())) && directory["versions"]!["D"]!.AsArray().Count == 16, "Diretório aponta a versão do grupo de cada clube da Série D");

        // A career in a group.
        var catalog = catalogs[6];
        var mine = catalog.Clubs[2].Id;
        var g = Game.New(mine, catalog);
        Check(g.SerieD != null && g.SerieD.Group == 7 && g.Clubs.Count == 6 && g.SerieD.Others.Count == 90 && g.SeasonRounds == 22, "Carreira da Série D joga o grupo de seis e conhece os outros 90 clubes");
        Check(g.Fixtures.Count == 30 && g.Fixtures.Min(f => f.Round) == 1 && g.Fixtures.Max(f => f.Round) == 10 && g.Fixtures.All(f => f.Round >= 1), "Grupo joga 10 rodadas de ida e volta");
        Check(g.Fixtures.GroupBy(f => f.Round).All(r => r.Count() == 3 && r.SelectMany(f => new[] { f.Home, f.Away }).Distinct().Count() == 6), "Cada rodada tem três jogos e todos os clubes jogam");
        Check(g.Clubs.Where(c => c.Id != mine).All(c => g.OpponentSquads.ContainsKey(c.Id)) && !g.SerieD.Others.Any(c => g.OpponentSquads.ContainsKey(c.Id)), "Só os cinco adversários do grupo têm elenco; os outros 90 clubes não");
        Check(g.Life!.Competitions.All(c => c.Id == "state") && g.Life.Board.TargetPosition == 4, "Série D tem só o circuito estadual e a meta é classificar-se entre os quatro primeiros");

        // Group stage. The state circuit shares the weeks, so every week is played through Advance or AdvanceWeek.
        void Week() { if (Game.HasLeagueMatch(g) || ClubCompetitions.NextDue(g) != null) Game.Advance(g); else Game.AdvanceWeek(g); }
        for (int i = 0; i < SerieD.GroupRounds; i++) Week();
        Check(g.Round == 11 && g.Fixtures.All(f => f.HomeGoals != null), "Dez semanas encerram a fase de grupos");
        var state = g.SerieD!;
        Check(state.Qualified.Count == 64 && state.Qualified.Distinct().Count() == 64 && state.Results.Count == 16, "Quatro por grupo (64 clubes) e as 16 tabelas finais ficam registradas");
        Check(state.Results.All(r => r.Table.Count == 6 && r.Table.All(t => t.Played == 10) && r.Table.Zip(r.Table.Skip(1)).All(p => p.First.Points >= p.Second.Points)), "Todas as tabelas têm seis clubes, dez jogos e estão ordenadas");
        var own = state.Results.Single(r => r.Group == 7).Table;
        Check(own.Select(r => r.ClubId).Order().SequenceEqual(g.Clubs.Select(c => c.Id).Order()) && own.Take(4).All(r => state.Qualified.Contains(r.ClubId)), "Tabela do grupo do clube vem dos jogos reais");
        var knockout = g.Life.Competitions.Single(c => c.Id == "serie-d");
        Check(knockout.Light && knockout.Legs == 2 && knockout.Matches.Count == 64 && knockout.Matches.Count(m => m.Leg == 1) == 32 && knockout.Rounds.SequenceEqual(SerieD.KnockoutRounds), "Fase final começa com 32 confrontos de ida e volta");
        var firstTie = knockout.Matches.Where(m => m.Leg == 1).Take(2).ToList();
        var seedOne = state.Qualified[0]; var seedLast = state.Qualified[63];
        Check(knockout.Matches.Any(m => m.Leg == 1 && new[] { m.Fixture.Home, m.Fixture.Away }.Contains(seedOne) && new[] { m.Fixture.Home, m.Fixture.Away }.Contains(seedLast)), "Melhor campanha enfrenta a pior classificada");

        // Everything else is played until the season ends.
        int guard = 0;
        while (g.Round <= g.SeasonRounds && guard++ < 40) Week();
        Check(g.Round == 23 && knockout.Champion != "", "Final decide o campeão na semana 22");
        var playoffs = g.Life.Competitions.Where(c => c.Id.StartsWith("serie-d-playoff-")).ToList();
        Check(playoffs.Count == 2 && playoffs.All(c => c.Promotion && c.Champion != "" && c.Matches.Count == 2), "Duas repescagens entre os eliminados nas quartas definem dois acessos");
        Check(state.Promoted.Count == 6 && state.Promoted.Distinct().Count() == 6 && state.Promoted.Contains(knockout.Champion), "Seis clubes sobem, sem repetição");
        var finalists = knockout.Matches.Where(m => m.Stage == 5 && m.Leg == 1).SelectMany(m => new[] { m.Fixture.Home, m.Fixture.Away }).ToList();
        Check(finalists.All(state.Promoted.Contains) && playoffs.All(c => state.Promoted.Contains(c.Champion)), "Sobem os finalistas e os vencedores da repescagem");
        Check(knockout.Matches.Where(m => m.Stage == 4 && m.Leg == 1).All(m => state.Promoted.Contains(m.Fixture.Home == m.Winner ? m.Fixture.Away : m.Fixture.Home) || finalists.Contains(m.Winner)), "Os semifinalistas eliminados também sobem");
        Check(knockout.Matches.All(m => m.Fixture.HomeGoals != null && m.Winner != ""), "Todas as partidas do mata-mata foram disputadas e têm vencedor");
        int created = g.SerieD.Others.Count(c => g.OpponentSquads.ContainsKey(c.Id));
        Check(created <= 12, "Elencos só foram criados para adversários realmente enfrentados (" + created + " de 90)");
        Check(knockout.Matches.Where(m => m.Fixture.Setup == null).All(m => m.Fixture.Home != mine && m.Fixture.Away != mine), "Jogos do clube usam elencos; os demais são resolvidos pela força");


        // A club that reaches the final phase meets opponents it has never seen: their squads appear only then.
        for (int group = 1; group <= 16; group++)
        {
            var strongest = catalogs[group - 1].Clubs.OrderByDescending(c => c.Strength).First();
            var run = Game.New(strongest.Id, catalogs[group - 1]); run.Life!.Competitions.Clear();
            for (int i = 0; i < SerieD.GroupRounds; i++) Game.Advance(run);
            if (!run.SerieD!.Qualified.Contains(strongest.Id)) continue;
            Check(!run.SerieD.Others.Any(c => run.OpponentSquads.ContainsKey(c.Id)), "Ao fim do grupo nenhum elenco de outro grupo existe ainda");
            int guard2 = 0;
            while (run.Round <= run.SeasonRounds && guard2++ < 40) { if (Game.HasLeagueMatch(run) || ClubCompetitions.NextDue(run) != null) Game.Advance(run); else Game.AdvanceWeek(run); }
            var played = run.Life.Competitions.SelectMany(c => c.Matches).Where(m => m.Fixture.Home == strongest.Id || m.Fixture.Away == strongest.Id).ToList();
            Check(played.Count >= 2 && played.All(m => m.Fixture.Setup != null && m.Fixture.HomeGoals != null), "Jogos do clube no mata-mata usam elencos completos");
            Check(run.SerieD.Others.Count(c => run.OpponentSquads.ContainsKey(c.Id)) >= 1, "Elenco do adversário é criado quando o clube o enfrenta");
            break;
        }

        // The next season starts again from round 1.
        TransferMarket.NextSeason(g);
        Check(g.Season == 2027 && g.Round == 1 && g.SeasonRounds == 22 && g.Fixtures.Count == 30 && g.SerieD!.Qualified.Count == 0 && g.Life.Competitions.All(c => c.Id == "state"), "Nova temporada recomeça na fase de grupos");

        // A career whose club does not reach the final phase still sees it played.
        var out_ = Game.New(catalogs[0].Clubs[0].Id, catalogs[0]);
        out_.Life!.Competitions.Clear();
        for (int i = 0; i < SerieD.GroupRounds; i++) Game.Advance(out_);
        Check(out_.Life.Competitions.Any(c => c.Id == "serie-d"), "Fase final é criada ao fim do grupo, com ou sem o clube");
    }
}

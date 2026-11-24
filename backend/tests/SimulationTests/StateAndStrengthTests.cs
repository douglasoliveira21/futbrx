using Brasileirao;

// State championship of the club's own state, and club strength shaping the squads.
public static class StateAndStrengthTests
{
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }
    static double Average(IEnumerable<Player> squad) => squad.Average(p => p.Rating);

    public static void Run()
    {
        // The club's state: SP for a Série A club of São Paulo.
        var catalog = Game.Candidate2026Catalog();
        var g = Game.New("pal", catalog);
        var state = g.Life!.Competitions.Single(c => c.Id == "state");
        Check(SeriesFormats.StateOf("pal") == "SP" && state.Name == "Campeonato Paulista" && state.GroupStage && state.Light, "Clube de São Paulo disputa o Campeonato Paulista");
        var field = state.Matches.SelectMany(m => new[] { m.Fixture.Home, m.Fixture.Away }).Distinct().ToList();
        Check(field.Count == 8 && field.Contains("pal") && state.Matches.Count == 28 && field.Count(id => SeriesFormats.StateOf(id) == "SP") == 8, "Oito clubes paulistas, em turno único (28 jogos)");
        Check(state.Matches.All(m => m.Fixture.Round is >= 2 and <= 8) && state.Matches.GroupBy(m => m.Fixture.Round).All(r => r.Count() == 4) && field.All(id => state.Matches.Count(m => m.Fixture.Home == id || m.Fixture.Away == id) == 7), "Sete rodadas, quatro jogos por rodada, cada clube joga sete vezes");
        Check(field.Where(id => g.Clubs.All(c => c.Id != id)).All(id => g.KnownClubs.Any(k => k.Id == id)) && g.KnownClubs.All(c => !g.OpponentSquads.ContainsKey(c.Id)), "Clubes de fora da liga são conhecidos pelo nome, sem elenco");
        Check(state.Matches.All(m => m.Fixture.Home != "pal" && m.Fixture.Away != "pal" || m.Fixture.CompetitionName == state.Name), "Jogos do clube levam o nome do campeonato");

        // A small state borrows clubs from its neighbours.
        var amapa = SerieD.Catalog(SerieD.GroupOf("trem"));
        var small = Game.New("trem", amapa);
        var smallState = small.Life!.Competitions.Single(c => c.Id == "state");
        Check(smallState.Name == "Campeonato Amapaense" && smallState.Matches.SelectMany(m => new[] { m.Fixture.Home, m.Fixture.Away }).Distinct().Count() == 8, "Estado com poucos clubes na base completa o campeonato com os mais próximos");

        // Playing it: the first phase ends, the best four play semifinals and the final.
        for (int i = 0; i < 40 && state.Champion == "" && g.Round <= 12; i++)
        {
            foreach (var p in g.Squad) { p.InjuryDays = 0; p.SuspensionMatches = 0; }
            Sports.RepairSelection(g.Squad, true);
            if (Game.HasLeagueMatch(g) || ClubCompetitions.NextDue(g) != null) Game.Advance(g); else Game.AdvanceWeek(g);
        }
        var final = g.Life.Competitions.Single(c => c.Id == "state-final");
        Check(state.Advanced.Count == 4 && final.Legs == 2 && final.Matches.Count(m => m.Stage == 0) == 4 && final.Light, "Os quatro primeiros do estadual avançam às semifinais de ida e volta");
        for (int i = 0; i < 40 && final.Champion == ""; i++)
        {
            foreach (var p in g.Squad) { p.InjuryDays = 0; p.SuspensionMatches = 0; }
            Sports.RepairSelection(g.Squad, true);
            if (Game.HasLeagueMatch(g) || ClubCompetitions.NextDue(g) != null) Game.Advance(g); else Game.AdvanceWeek(g);
        }
        Check(final.Champion != "" && state.Advanced.Contains(final.Champion) && final.Matches.All(m => m.Winner != ""), "Final do estadual define o campeão entre os quatro classificados");
        var met = g.Life.Competitions.SelectMany(c => c.Matches).Select(m => m.Fixture).Where(f => f.Home == "pal" || f.Away == "pal").SelectMany(f => new[] { f.Home, f.Away }).ToHashSet();
        Check(g.OpponentSquads.Keys.Where(id => g.KnownClubs.Any(k => k.Id == id)).All(met.Contains), "Só os adversários que o clube realmente enfrentou ganham elenco");

        // Club strength shapes the squads.
        var a = Game.New("pal", Game.Clubs2026Catalog()); var b = Game.New(Game.SeriesCatalog("B").Clubs[0].Id, Game.SeriesCatalog("B"));
        var c = Game.New(Game.SeriesCatalog("C").Clubs[0].Id, Game.SeriesCatalog("C")); var d = Game.New(SerieD.Catalog(3).Clubs[0].Id, SerieD.Catalog(3));
        double ra = Average(a.Squad), rb = Average(b.Squad), rc = Average(c.Squad), rd = Average(d.Squad);
        Check(ra > rb && rb > rc && rc > rd, $"Elencos mais fortes nas séries mais altas (A {ra:0.0}, B {rb:0.0}, C {rc:0.0}, D {rd:0.0})");
        var strongD = SerieD.Catalog(3).Clubs.OrderByDescending(x => x.Strength).First(); var weakD = SerieD.Catalog(3).Clubs.OrderBy(x => x.Strength).First();
        var cat = SerieD.Catalog(3);
        double Rating(string id) => cat.Players.Where(p => p.ClubId == id).Average(p => p.Player.Rating);
        Check(strongD.Strength > weakD.Strength && Rating(strongD.Id) > Rating(weakD.Id), "Dentro da mesma série, o clube mais forte tem o elenco melhor");
        Check(cat.Players.All(p => p.Player.Rating is >= 1 and <= 99) && Average(Game.New("pal").Squad) == Average(Game.DemoCatalog().Players.Where(p => p.ClubId == "pal").Select(p => p.Player)), "Notas continuam válidas e a base demonstrativa não muda");
    }
}

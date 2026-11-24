using Brasileirao;

// Formats and movement of Séries A to D (2026 regulations): playoffs of the Série B, the two phases of the Série C,
// and promotion and relegation of the managed club.
public static class SeriesMovementTests
{
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }
    static string Series(GameState g) => SeriesMovement.SeriesOf(g) ?? "-";
    static GameState Career(string series, int index = 0)
    {
        var catalog = series switch { "A" => Game.Clubs2026Catalog(), "D" => SerieD.Catalog(3), _ => Game.SeriesCatalog(series) };
        return Game.New(catalog.Clubs[index].Id, catalog);
    }
    // League by decree: the club wins (win=true), loses (false) or draws (null) every match; the others draw 0-0.
    static void DecreeLeague(GameState g, bool? win)
    {
        g.Life!.Competitions.Clear();
        foreach (var f in g.Fixtures)
        {
            bool mine = f.Home == g.ClubId || f.Away == g.ClubId, home = f.Home == g.ClubId;
            int mineGoals = win == true ? 1 : 0, otherGoals = win == false ? 1 : 0;
            f.HomeGoals = mine ? (home ? mineGoals : otherGoals) : 0;
            f.AwayGoals = mine ? (home ? otherGoals : mineGoals) : 0;
        }
        g.Round = g.SeasonRounds;
        SeriesFormats.AfterRound(g);
    }
    // Plays every pending match of every competition by decree until nothing is left: the club wins (or loses) its
    // own matches, and in the others the home club of the first leg wins 1-0.
    static void DecreeCompetitions(GameState g, bool win)
    {
        for (int pass = 0; pass < 20; pass++)
        {
            var pending = g.Life!.Competitions.SelectMany(c => c.Matches).Where(m => m.Fixture.HomeGoals == null).OrderBy(m => m.Fixture.Round).ThenBy(m => m.Leg).ToList();
            if (pending.Count == 0) break;
            foreach (var m in pending)
            {
                var f = m.Fixture; bool mine = f.Home == g.ClubId || f.Away == g.ClubId, home = f.Home == g.ClubId;
                bool homeWins = mine ? home == win : m.Leg == 1;
                // A second leg of other clubs ends 0-0 so that the first leg decides; the club's own legs are decisive.
                f.HomeGoals = homeWins ? 1 : 0; f.AwayGoals = homeWins ? 0 : (mine ? 1 : 0);
                if (!mine && m.Leg == 2) { f.HomeGoals = 0; f.AwayGoals = 0; }
                ClubCompetitions.Complete(g, m);
            }
        }
        g.Round = g.SeasonRounds + 1;
    }
    // Random cards and injuries may leave a short test squad without eleven eligible players.
    static void Heal(GameState g) { foreach (var p in g.Squad) { p.InjuryDays = 0; p.SuspensionMatches = 0; } Sports.RepairSelection(g.Squad, true); }
    static int Position(GameState g) => LeagueRules.Table(g.Clubs, g.Fixtures, g.LotterySeed + ":" + g.Season).FindIndex(r => r.Club.Id == g.ClubId) + 1;
    static void Season(GameState g, bool? win)
    {
        DecreeLeague(g, win);
        DecreeCompetitions(g, win != false);
        TransferMarket.NextSeason(g);
    }

    public static void Run()
    {
        // Calendar of each series.
        var a = Career("A"); var b = Career("B"); var c = Career("C");
        Check(a.SeasonRounds == 38 && a.Fixtures.Count == 380 && b.SeasonRounds == 40 && b.Fixtures.Count == 380 && b.Fixtures.Max(f => f.Round) == 38, "Séries A e B têm 38 rodadas de liga; a B reserva duas semanas aos playoffs");
        Check(c.SeasonRounds == 27 && c.Fixtures.Count == 190 && c.Fixtures.Max(f => f.Round) == 19, "Série C joga a primeira fase em turno único (19 rodadas, 190 jogos)");
        Check(c.Fixtures.GroupBy(f => f.Round).All(r => r.Count() == 10) && c.Clubs.All(club => c.Fixtures.Count(f => f.Home == club.Id || f.Away == club.Id) == 19), "Cada clube da C enfrenta os outros 19 uma única vez");

        // Série A: the last three go down.
        Check(SeriesMovement.Destination(a, "A", 20) == "B" && SeriesMovement.Destination(a, "A", 17) == "B" && SeriesMovement.Destination(a, "A", 16) == null && SeriesMovement.Destination(a, "A", 1) == null, "Série A rebaixa os quatro últimos e não tem acesso");
        var aId = a.ClubId; var strongestB = Game.SeriesCatalog("B").Clubs.OrderByDescending(x => x.Strength).ThenBy(x => x.Id, StringComparer.Ordinal).First().Id;
        Season(a, false);
        Check(Series(a) == "B" && a.Clubs.Count == 20 && a.Clubs.Any(x => x.Id == aId) && a.Clubs.All(x => x.Id != strongestB) && a.SeasonRounds == 40 && a.Fixtures.Count == 380, "Rebaixado da A joga a Série B no lugar do clube mais forte");
        Check(a.Clubs.Where(x => x.Id != aId).All(x => a.OpponentSquads.ContainsKey(x.Id)) && a.Life!.Competitions.Any(x => x.Id == "copa") && a.Life.Board.Confidence < 70, "Elencos da nova divisão existem, copas voltam e a diretoria reage ao rebaixamento");
        Heal(a);
        Game.Advance(a);
        Check(a.Round == 2 && a.Fixtures.Count(f => f.HomeGoals != null) == 10, "Primeira rodada da Série B é disputada");

        // Série B: first two go up directly; 3rd to 6th play playoffs; the last four go down.
        Check(SeriesMovement.Destination(b, "B", 17) == "C" && SeriesMovement.Destination(b, "B", 16) == null && SeriesMovement.Destination(b, "B", 2) == "A" && SeriesMovement.Destination(b, "B", 7) == null, "Série B rebaixa quatro e promove os dois primeiros");
        DecreeLeague(b, null);
        var table = LeagueRules.Table(b.Clubs, b.Fixtures, b.LotterySeed + ":" + b.Season).Select(r => r.Club.Id).ToList();
        var playoffs = b.Life!.Competitions.Where(x => x.Id.StartsWith("serie-b-playoff-")).ToList();
        Check(playoffs.Count == 2 && playoffs.All(x => x.Legs == 2 && x.KeepOrder && x.NoPenalties && x.Promotion && x.Matches.Count == 2), "Fim da liga cria dois playoffs de ida e volta, sem pênaltis");
        var first = playoffs[0].Matches.Single(m => m.Leg == 1).Fixture; var second = playoffs[0].Matches.Single(m => m.Leg == 2).Fixture;
        var second2 = playoffs[1].Matches.Single(m => m.Leg == 1).Fixture;
        Check(first.Home == table[5] && first.Away == table[2] && second.Home == table[2] && second2.Home == table[4] && second2.Away == table[3] && first.Round == 39 && second.Round == 40, "3º × 6º e 4º × 5º: o pior colocado recebe a ida e o melhor a volta");
        // A level aggregate favours the better placed club (host of the second leg).
        var probe = Career("B"); DecreeLeague(probe, null);
        var po = probe.Life!.Competitions.First(x => x.Id.StartsWith("serie-b-playoff-"));
        var l1 = po.Matches.Single(m => m.Leg == 1); var l2 = po.Matches.Single(m => m.Leg == 2);
        l1.Fixture.HomeGoals = 1; l1.Fixture.AwayGoals = 1; ClubCompetitions.Complete(probe, l1);
        l2.Fixture.HomeGoals = 2; l2.Fixture.AwayGoals = 2; ClubCompetitions.Complete(probe, l2);
        Check(l2.Winner == l2.Fixture.Home && po.Champion == l2.Fixture.Home && l2.Fixture.HomePenalties == null, "Agregado empatado no playoff dá a vaga ao mais bem colocado, sem pênaltis");
        int position = Position(b); bool inPlayoff = playoffs.Any(x => x.Matches.Any(m => m.Fixture.Home == b.ClubId || m.Fixture.Away == b.ClubId));
        var bId = b.ClubId; DecreeCompetitions(b, true); TransferMarket.NextSeason(b);
        bool expectUp = position <= 2 || inPlayoff;
        Check(Series(b) == (expectUp ? "A" : position >= 17 ? "C" : "B") && (!expectUp || b.Clubs.Any(x => x.Id == bId)), "Série B: sobe quem é 1º ou 2º ou vence o playoff; cai quem fica de 17º a 20º");
        var champ = Career("B"); Season(champ, true);
        Check(Series(champ) == "A" && champ.DataVersion == Game.Clubs2026Version && champ.Clubs.Count == 20, "Campeão da B sobe para a Série A");
        var down = Career("B", 1); Season(down, false);
        Check(Series(down) == "C" && down.SeasonRounds == 27 && down.Fixtures.Count == 190, "Rebaixado da B joga a Série C em turno único");

        // Série C: the best eight play two groups; the two best of each group go up; the group winners play the final.
        DecreeLeague(c, null);
        var ct = LeagueRules.Table(c.Clubs, c.Fixtures, c.LotterySeed + ":" + c.Season).Select(r => r.Club.Id).ToList();
        var groups = c.Life!.Competitions.Single(x => x.Id == "serie-c-groups");
        var g1 = groups.Matches.Where(m => m.Tie == "g1").SelectMany(m => new[] { m.Fixture.Home, m.Fixture.Away }).Distinct().Order().ToList();
        var g2 = groups.Matches.Where(m => m.Tie == "g2").SelectMany(m => new[] { m.Fixture.Home, m.Fixture.Away }).Distinct().Order().ToList();
        Check(groups.GroupStage && groups.Matches.Count == 24 && g1.SequenceEqual(new[] { ct[0], ct[3], ct[4], ct[7] }.Order()) && g2.SequenceEqual(new[] { ct[1], ct[2], ct[5], ct[6] }.Order()), "Segunda fase: grupos 1º/4º/5º/8º e 2º/3º/6º/7º, com doze jogos cada");
        Check(groups.Matches.All(m => m.Fixture.Round is >= 20 and <= 25 && m.Fixture.Leg == 0) && groups.Matches.GroupBy(m => m.Fixture.Round).All(r => r.Count() == 4), "Seis rodadas, duas partidas por grupo em cada uma");
        Check(new[] { g1, g2 }.All(grp => grp.All(id => groups.Matches.Count(m => m.Fixture.Home == id) == 3 && groups.Matches.Count(m => m.Fixture.Away == id) == 3)), "Cada clube joga três vezes em casa e três fora");
        int cp = ct.IndexOf(c.ClubId) + 1; var cId = c.ClubId;
        DecreeCompetitions(c, true);
        Check(groups.Advanced.Count == 4 && c.Life.Competitions.Any(x => x.Id == "serie-c-final" && x.Legs == 2 && x.Matches.Count == 2 && x.Matches[0].Fixture.Round == 26), "Dois por grupo sobem e os líderes disputam a final em ida e volta");
        TransferMarket.NextSeason(c);
        Check(Series(c) == (cp <= 8 ? "B" : cp >= 19 ? "D" : "C"), "Série C: sobe quem é top 2 do seu grupo; caem os dois últimos da primeira fase");
        var cup = Career("C"); Season(cup, true);
        Check(Series(cup) == "B" && cup.SerieD == null && cup.Clubs.Count == 20 && cup.SeasonRounds == 40, "Campeão da C sobe para a Série B");
        var cdown = Career("C", 2); var cdId = cdown.ClubId; Season(cdown, false);
        Check(Series(cdown) == "D" && cdown.SerieD != null && cdown.Clubs.Count == 6 && cdown.SeasonRounds == 22 && cdown.Fixtures.Count == 30 && cdown.Clubs.Any(x => x.Id == cdId), "Rebaixado da C joga um grupo da Série D, com seis clubes e 22 semanas");
        Check(SeriesMovement.Destination(c, "C", 19) == "D" && SeriesMovement.Destination(c, "C", 18) == null, "Série C rebaixa apenas os dois últimos");
        Check(cdown.Clubs.Where(x => x.Id != cdId).All(x => cdown.OpponentSquads.ContainsKey(x.Id)) && cdown.Life!.Competitions.All(x => x.Id == "state") && cdown.Life.Board.TargetPosition == 4, "Série D: elencos do grupo, sem copas nacionais e meta entre os quatro primeiros");
        for (int i = 0; i < SerieD.GroupRounds; i++) { Heal(cdown); Game.Advance(cdown); }
        Check(cdown.SerieD!.Qualified.Count == 64 && cdown.Life.Competitions.Any(x => x.Id == "serie-d"), "Clube rebaixado completa a fase de grupos da Série D e a fase final é criada");

        // Round trip: down from A, back up from B.
        var t = Career("A", 5); var tId = t.ClubId; Season(t, false); Season(t, true);
        Check(Series(t) == "A" && t.Season == 2028 && t.Clubs.Any(x => x.Id == tId), "Cai da A e volta pela Série B na temporada seguinte");

        var demo = Game.New("pal");
        Check(SeriesMovement.SeriesOf(demo) == null && demo.SeasonRounds == 38, "Carreira demonstrativa não participa da escada de divisões");
    }
}

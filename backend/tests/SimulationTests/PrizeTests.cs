using Brasileirao;

// League, state, regional cup, Supercopa and Série D prizes.
public static class PrizeTests
{
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }
    const long Mi = 1_000_000;
    static GameState Career(string series, string club)
    {
        var catalog = series switch { "A" => Game.Clubs2026Catalog(), _ => Game.SeriesCatalog(series) };
        return Game.New(club, catalog);
    }

    public static void Run()
    {
        var a = Career("A", "pal"); var b = Career("B", Game.SeriesCatalog("B").Clubs[0].Id); var c = Career("C", Game.SeriesCatalog("C").Clubs[0].Id); var demo = Game.New("pal");
        Check(SeriesFormats.LeaguePrize(a, 1) == 54 * Mi && SeriesFormats.LeaguePrize(a, 2) == 50 * Mi && SeriesFormats.LeaguePrize(a, 12) == 21 * Mi && SeriesFormats.LeaguePrize(a, 16) == 13 * Mi && SeriesFormats.LeaguePrize(a, 17) == 0 && SeriesFormats.LeaguePrize(a, 20) == 0, "Série A paga de R$ 54 mi (1º) a R$ 13 mi (16º) e nada do 17º ao 20º");
        Check(SeriesFormats.LeaguePrize(b, 1) == 6 * Mi && SeriesFormats.LeaguePrize(b, 4) == 2_500_000 && SeriesFormats.LeaguePrize(b, 8) == 600_000 && SeriesFormats.LeaguePrize(b, 9) == 0, "Série B paga de R$ 6 mi (campeão) a R$ 600 mil (8º)");
        Check(SeriesFormats.LeaguePrize(c, 1) == 0 && SeriesFormats.LeaguePrize(demo, 1) == 10 * Mi, "Série C paga por fase; a base demonstrativa mantém a tabela antiga");

        // State championships: champion, runner-up and the cota of the main clubs.
        var sp = a.Life!.Competitions.Single(x => x.Id == "state"); var rj = Career("A", "fla").Life!.Competitions.Single(x => x.Id == "state");
        Check(sp.Prize == 5 * Mi && sp.RunnerUpPrize == 1_650_000 && sp.EntryPrize == 35 * Mi, "Paulista: campeão R$ 5 mi, vice R$ 1,65 mi e cota de R$ 35 mi para clube da Série A");
        Check(rj.Prize == 10 * Mi && rj.RunnerUpPrize == 5_500_000 && rj.EntryPrize == 8_300_000, "Carioca: campeão R$ 10 mi, vice R$ 5,5 mi e cota de R$ 8,3 mi");
        var spB = Career("B", "novorizontino"); 
        Check(spB.Life!.Competitions.Single(x => x.Id == "state").EntryPrize == 0 && Career("C", "anapolis").Life!.Competitions.Single(x => x.Id == "state").Name == "Campeonato Goiano", "Cota dos principais clubes só vale para a Série A");
        Check(Career("B", "cuiaba").Life!.Competitions.Single(x => x.Id == "state").Prize == 150_000, "Estadual de Mato Grosso paga R$ 150 mil ao campeão");
        // Paying the cota and the prizes of the state cup.
        var m = sp.Matches.First(x => x.Fixture.Home == "pal" || x.Fixture.Away == "pal"); long before = a.Balance;
        m.Fixture.HomeGoals = 0; m.Fixture.AwayGoals = 0; ClubCompetitions.Complete(a, m);
        Check(a.Balance == before + 35 * Mi && a.Ledger.Count(l => l.Description.Contains("cota de participação")) == 1, "Cota de participação é paga uma vez, no primeiro jogo do clube");
        var m2 = sp.Matches.Where(x => x != m && (x.Fixture.Home == "pal" || x.Fixture.Away == "pal")).First(); before = a.Balance;
        m2.Fixture.HomeGoals = 1; m2.Fixture.AwayGoals = 1; ClubCompetitions.Complete(a, m2);
        Check(a.Balance == before, "Cota de participação não é paga de novo");

        // Copa do Nordeste and Copa Verde.
        var ne = Career("A", "bah").Life!.Competitions.Single(x => x.Id == "copa-nordeste");
        Check(ne.Light && ne.EntryPrize == 2_500_000 && ne.StagePrizes.SequenceEqual([500_000, 600_000, 0]) && ne.RunnerUpPrize == 700_000 && ne.Prize == 1 * Mi && ne.Matches.Count == 4, "Copa do Nordeste: grupo 1, quartas R$ 500 mil, semifinal R$ 600 mil, vice R$ 700 mil, campeão R$ 1 mi");
        Check(Career("B", "ceara").Life!.Competitions.Single(x => x.Id == "copa-nordeste").EntryPrize == 1_350_000 && Career("C", "botafogo-pb").Life!.Competitions.Single(x => x.Id == "copa-nordeste").EntryPrize == 850_000, "Participação na Copa do Nordeste segue o grupo da série (B: R$ 1,35 mi; C: R$ 850 mil)");
        var verde = Career("C", "amazonas").Life!.Competitions.Single(x => x.Id == "copa-verde");
        Check(verde.EntryPrize == 100_000 && verde.StagePrizes.SequenceEqual([100_000, 250_000, 0]) && verde.RunnerUpPrize == 250_000 && verde.Prize == 400_000, "Copa Verde: cotas de R$ 100 mil, R$ 250 mil, vice R$ 250 mil e campeão R$ 400 mil");
        Check(!a.Life.Competitions.Any(x => x.Id is "copa-nordeste" or "copa-verde") && !Career("A", "bah").Life.Competitions.Any(x => x.Id == "copa-verde"), "Cada clube só disputa a copa regional da sua região");
        var cupGame = Career("A", "bah"); var cup = cupGame.Life!.Competitions.Single(x => x.Id == "copa-nordeste"); before = cupGame.Balance;
        for (int pass = 0; pass < 4 && cup.Champion == ""; pass++)
            foreach (var t in cup.Matches.Where(x => x.Stage == cup.Stage && x.Fixture.HomeGoals == null).ToList())
            {
                bool mine = t.Fixture.Home == "bah" || t.Fixture.Away == "bah";
                bool homeWins = mine ? t.Fixture.Home == "bah" : true;
                t.Fixture.HomeGoals = homeWins ? 1 : 0; t.Fixture.AwayGoals = homeWins ? 0 : 1; ClubCompetitions.Complete(cupGame, t);
            }
        Check(cup.Champion == "bah" && cupGame.Balance - before == 4_600_000, "Campeão da Copa do Nordeste pelo grupo 1 recebe o máximo acumulado de R$ 4,6 mi");

        // Supercopa: the league champion against the Copa do Brasil champion of the previous season.
        var s = Career("A", "pal"); s.Life!.Competitions.Clear();
        s.PastSeasons.Add(new PastSeason { Season = 2025, Champion = "pal", Table = [new("pal", 80, 38, 24, 8, 6, 60, 30, 30), new("fla", 78, 38, 23, 9, 6, 58, 30, 28)] });
        s.Life.CupHistory.Add(new ArchivedCompetition(2025, new ClubCompetition { Id = "copa", Champion = "fla" }));
        SeriesFormats.CreateSupercopa(s);
        var sc = s.Life.Competitions.Single(x => x.Id == "supercopa");
        Check(sc.Prize == 11_640_000 && sc.RunnerUpPrize == 6_350_000 && sc.Matches.Count == 1 && new[] { sc.Matches[0].Fixture.Home, sc.Matches[0].Fixture.Away }.Order().SequenceEqual(["fla", "pal"]), "Supercopa: campeão do Brasileirão × campeão da Copa do Brasil, R$ 11,64 mi e R$ 6,35 mi");
        var same = Career("A", "pal"); same.Life!.Competitions.Clear();
        same.PastSeasons.Add(new PastSeason { Season = 2025, Champion = "pal", Table = [new("pal", 80, 38, 24, 8, 6, 60, 30, 30), new("fla", 78, 38, 23, 9, 6, 58, 30, 28)] });
        same.Life.CupHistory.Add(new ArchivedCompetition(2025, new ClubCompetition { Id = "copa", Champion = "pal" }));
        SeriesFormats.CreateSupercopa(same);
        Check(same.Life.Competitions.Single(x => x.Id == "supercopa").Matches[0].Fixture is var f && new[] { f.Home, f.Away }.Contains("fla"), "Campeão duplo enfrenta o vice do Brasileirão na Supercopa");
        var none = Career("A", "bot"); none.Life!.Competitions.Clear(); none.PastSeasons.Add(new PastSeason { Season = 2025, Champion = "pal", Table = [] }); none.Life.CupHistory.Add(new ArchivedCompetition(2025, new ClubCompetition { Id = "copa", Champion = "fla" }));
        SeriesFormats.CreateSupercopa(none);
        Check(!none.Life.Competitions.Any(x => x.Id == "supercopa"), "Clube que não é campeão não disputa a Supercopa");

        // Série D: elimination prizes of the final phase.
        var d = Game.New(SerieD.Catalog(3).Clubs[0].Id, SerieD.Catalog(3)); d.Life!.Competitions.Clear();
        for (int i = 0; i < SerieD.GroupRounds; i++) { foreach (var p in d.Squad) { p.InjuryDays = 0; p.SuspensionMatches = 0; } Sports.RepairSelection(d.Squad, true); Game.Advance(d); }
        var ko = d.Life.Competitions.Single(x => x.Id == "serie-d");
        Check(ko.Prize == 800_000 && ko.RunnerUpPrize == 500_000 && ko.LoserPrizes.SequenceEqual([0, 0, 0, 93_750, 275_000, 0]), "Série D: campeão R$ 800 mil, vice R$ 500 mil, semifinalistas e quartas eliminados com cotas próprias");
        Check(d.Life.Competitions.Where(x => x.Id.StartsWith("serie-d-playoff")).All(x => x.Prize == 0), "Repescagem da Série D não paga prêmio em dinheiro");
    }
}

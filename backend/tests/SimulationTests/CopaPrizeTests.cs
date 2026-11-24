using Brasileirao;

// Copa do Brasil 2026 cotas (CBF), accumulated by phase, by series of the club.
public static class CopaPrizeTests
{
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }
    // Plays the cup by decree: the club wins every tie until the final, where it wins or loses.
    static long CupMoney(GameState g, bool winFinal)
    {
        var copa = g.Life!.Competitions.Single(c => c.Id == "copa");
        long before = g.Ledger.Where(l => l.Description.StartsWith("Copa do Brasil") || l.Description.StartsWith("Título")).Sum(l => l.Amount);
        for (int pass = 0; pass < 6 && copa.Champion == ""; pass++)
        {
            bool final = copa.Matches.Count(m => m.Stage == copa.Stage) == 1;
            foreach (var m in copa.Matches.Where(m => m.Stage == copa.Stage && m.Fixture.HomeGoals == null).ToList())
            {
                var f = m.Fixture; bool mine = f.Home == g.ClubId || f.Away == g.ClubId; bool homeWins = mine ? (f.Home == g.ClubId) == (!final || winFinal) : true;
                f.HomeGoals = homeWins ? 1 : 0; f.AwayGoals = homeWins ? 0 : 1;
                ClubCompetitions.Complete(g, m);
            }
        }
        return g.Ledger.Where(l => l.Description.StartsWith("Copa do Brasil") || l.Description.StartsWith("Título")).Sum(l => l.Amount) - before;
    }
    static GameState Career(string series)
    {
        var catalog = series switch { "A" => Game.Clubs2026Catalog(), _ => Game.SeriesCatalog(series) };
        return Game.New(catalog.Clubs[0].Id, catalog);
    }

    public static void Run()
    {
        long mi = 1_000_000;
        var a = Career("A"); var copa = a.Life!.Competitions.Single(c => c.Id == "copa");
        Check(copa.Prize == 78 * mi && copa.RunnerUpPrize == 34 * mi && copa.StagePrizes.SequenceEqual([5 * mi, 4 * mi, 9 * mi, 0]), "Copa do Brasil: campeão R$ 78 mi, vice R$ 34 mi e cotas de oitavas, quartas e semifinal");
        Check(CupMoney(a, true) == 96 * mi, "Série A campeã da Copa do Brasil recebe R$ 96 mi (2 + 3 + 4 + 9 + 78)");
        Check(CupMoney(Career("A"), false) == 52 * mi, "Série A vice-campeã recebe R$ 52 mi (2 + 3 + 4 + 9 + 34)");
        Check(CupMoney(Career("B"), true) == 100_590_000, "Série B campeã recebe R$ 100,59 mi (entra na 2ª fase)");
        Check(CupMoney(Career("C"), true) == 99_250_000, "Série C campeã recebe R$ 99,25 mi (entra na 1ª fase)");
        var early = Career("A"); var ec = early.Life!.Competitions.Single(c => c.Id == "copa");
        var first = ec.Matches.First(m => m.Fixture.Home == early.ClubId || m.Fixture.Away == early.ClubId);
        first.Fixture.HomeGoals = first.Fixture.Home == early.ClubId ? 0 : 1; first.Fixture.AwayGoals = first.Fixture.Home == early.ClubId ? 1 : 0;
        ClubCompetitions.Complete(early, first);
        Check(early.Ledger.Where(l => l.Description.StartsWith("Copa do Brasil")).Sum(l => l.Amount) == 5 * mi, "Eliminado nas oitavas recebe só a cota da fase e as anteriores (R$ 5 mi)");
    }
}

using Brasileirao;

// Promotion and relegation between Séries A to D, for the managed club.
public static class SeriesMovementTests
{
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }
    // Plays the season out by decree: the club wins (or loses) every match, the others draw 0-0.
    static void Finish(GameState g, bool win)
    {
        g.Life!.Competitions.Clear();
        foreach (var f in g.Fixtures)
        {
            bool mine = f.Home == g.ClubId || f.Away == g.ClubId;
            bool home = f.Home == g.ClubId;
            f.HomeGoals = mine ? (win == home ? 1 : 0) : 0;
            f.AwayGoals = mine ? (win == home ? 0 : 1) : 0;
        }
        g.Round = g.SeasonRounds + 1;
    }
    static string Series(GameState g) => SeriesMovement.SeriesOf(g) ?? "-";
    static GameState Career(string series, int index = 0)
    {
        var catalog = series switch { "A" => Game.Clubs2026Catalog(), "D" => SerieD.Catalog(3), _ => Game.SeriesCatalog(series) };
        return Game.New(catalog.Clubs[index].Id, catalog);
    }
    static void Season(GameState g, bool win) { Finish(g, win); TransferMarket.NextSeason(g); }

    public static void Run()
    {
        // Série A: the last positions go down to Série B, replacing its strongest club.
        var a = Career("A"); var aId = a.ClubId; var strongestB = Game.SeriesCatalog("B").Clubs.OrderByDescending(c => c.Strength).ThenBy(c => c.Id, StringComparer.Ordinal).First().Id;
        Check(Series(a) == "A" && SeriesMovement.Destination(a, "A", 20) == "B" && SeriesMovement.Destination(a, "A", 17) == "B" && SeriesMovement.Destination(a, "A", 16) == null && SeriesMovement.Destination(a, "A", 1) == null, "Série A rebaixa os quatro últimos e não tem acesso");
        Season(a, false);
        Check(Series(a) == "B" && a.Clubs.Count == 20 && a.Clubs.Any(c => c.Id == aId) && a.Clubs.All(c => c.Id != strongestB) && a.SeasonRounds == 38 && a.Fixtures.Count == 380, "Rebaixado da A joga a Série B no lugar do clube mais forte");
        Check(a.Clubs.Where(c => c.Id != aId).All(c => a.OpponentSquads.ContainsKey(c.Id)) && a.Life!.Competitions.Any(c => c.Id == "copa") && a.Life.Board.Confidence < 70, "Elencos da nova divisão existem, copas voltam e a diretoria reage ao rebaixamento");
        Sports.RepairSelection(a.Squad, true); // contracts that ended with the season may have left fewer than eleven starters
        Game.Advance(a);
        Check(a.Round == 2 && a.Fixtures.Count(f => f.HomeGoals != null) == 10, "Primeira rodada da Série B é disputada");

        // Série B: top four go up, bottom four go down.
        var b = Career("B"); var bId = b.ClubId; var weakestA = Game.Clubs2026Catalog().Clubs.OrderBy(c => c.Strength).ThenBy(c => c.Id, StringComparer.Ordinal).First().Id;
        Check(SeriesMovement.Destination(b, "B", 4) == "A" && SeriesMovement.Destination(b, "B", 5) == null && SeriesMovement.Destination(b, "B", 17) == "C", "Série B sobe quatro e rebaixa quatro");
        Season(b, true);
        Check(Series(b) == "A" && b.Clubs.Any(c => c.Id == bId) && b.Clubs.All(c => c.Id != weakestA) && b.Clubs.Count == 20 && b.DataVersion == Game.Clubs2026Version, "Campeão da B sobe para a Série A no lugar do clube mais fraco");
        var b2 = Career("B", 1); Season(b2, false);
        Check(Series(b2) == "C" && b2.Clubs.Count == 20 && b2.Clubs.Any(c => c.Id == b2.ClubId), "Rebaixado da B joga a Série C");

        // Série C: up to B, down to D (a group of six, chosen by state).
        var c = Career("C"); Season(c, true);
        Check(Series(c) == "B" && c.Clubs.Count == 20 && c.SerieD == null, "Campeão da C sobe para a Série B");
        var d = Career("C", 2); var dId = d.ClubId; Season(d, false);
        Check(Series(d) == "D" && d.SerieD != null && d.Clubs.Count == 6 && d.SeasonRounds == 22 && d.Fixtures.Count == 30 && d.Clubs.Any(x => x.Id == dId), "Rebaixado da C joga um grupo da Série D, com seis clubes e 22 semanas");
        Check(d.SerieD!.Others.Count == 90 - 0 || d.SerieD.Others.Count >= 89, "Série D conhece os outros clubes pelo nome");
        Check(d.Clubs.Where(x => x.Id != dId).All(x => d.OpponentSquads.ContainsKey(x.Id)) && d.Life!.Competitions.All(x => x.Id == "state") && d.Life.Board.TargetPosition == 4, "Série D: elencos do grupo, sem copas nacionais e meta entre os quatro primeiros");
        for (int i = 0; i < SerieD.GroupRounds; i++) { Sports.RepairSelection(d.Squad, true); Game.Advance(d); }
        Check(d.SerieD.Qualified.Count == 64 && d.Life.Competitions.Any(x => x.Id == "serie-d"), "Clube rebaixado completa a fase de grupos da Série D e a fase final é criada");

        // Round trip: down from A, back up from B.
        var t = Career("A", 5); var tId = t.ClubId; Season(t, false); Season(t, true);
        Check(Series(t) == "A" && t.Season == 2028 && t.Clubs.Any(x => x.Id == tId), "Cai da A e volta pela Série B na temporada seguinte");

        // The middle of the table stays; demonstration careers have no ladder.
        var s = Career("A", 2);
        Check(SeriesMovement.Destination(s, "A", 10) == null, "Posição intermediária mantém o clube na divisão");
        var demo = Game.New("pal");
        Check(SeriesMovement.SeriesOf(demo) == null, "Carreira demonstrativa não participa da escada de divisões");
    }
}

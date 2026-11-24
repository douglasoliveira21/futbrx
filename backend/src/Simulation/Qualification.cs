namespace Brasileirao;

// Distribution of the Brazilian places in the Libertadores, Pré-Libertadores and Sul-Americana. It resolves every combination
// of champions, positions and duplicated places from the results of all competitions, with the regulation of each season.
public static class Qualification
{
    public const string Libertadores = "libertadores", Pre = "pre-libertadores", Sula = "sul-americana";

    // Regulation by season: the first entry is the one in force from 2026 (competitions of 2027 included). Add an entry to change
    // the rules from a later season on.
    public static readonly List<QualificationRules> Rules = [new(2026, 4, 1, 6, true)];
    public static QualificationRules RulesFor(int season) => Rules.Where(r => r.FromSeason <= season).OrderBy(r => r.FromSeason).LastOrDefault() ?? Rules[0];

    // Pure function. `table` is the final league table (best first); `eligible` says which clubs may play the international cups
    // (the ones that stay in the first division); the cup finalists and the continental champions come as club ids (empty if unknown).
    public static List<Berth> Resolve(IReadOnlyList<string> table, Func<string, bool> eligible, string copaChampion, string copaRunnerUp, IEnumerable<string> continentalChampions, QualificationRules rules)
    {
        var order = table.Where(eligible).ToList();
        var berths = new List<Berth>(); var used = new HashSet<string>();
        int Pos(string club) => table.ToList().IndexOf(club) + 1;
        bool Add(string club, string competition, string origin)
        {
            if (club == "" || !used.Add(club)) return false;
            berths.Add(new(club, competition, origin)); return true;
        }
        string? NextFree() => order.FirstOrDefault(c => !used.Contains(c));

        // 1. Direct places by league position.
        var direct = order.Take(rules.LeagueDirect).ToList();
        foreach (var club in direct) Add(club, Libertadores, $"Brasileirão ({Pos(club)}º)");

        // 2. Copa do Brasil champion: group stage. If it already has a place by the league, the cup place goes to the runner-up.
        bool championAlreadyIn = copaChampion != "" && direct.Contains(copaChampion);
        if (copaChampion != "" && eligible(copaChampion) && !championAlreadyIn) Add(copaChampion, Libertadores, "Copa do Brasil (campeão)");

        // 3. Champions of the Libertadores and Sul-Americana: additional places; a champion that is already in frees the place
        //    to the next club of the table (the league zone expands).
        if (rules.ContinentalChampionsDirect)
            foreach (var champion in continentalChampions.Where(c => c != "" && eligible(c)))
            {
                if (used.Contains(champion)) { var next = NextFree(); if (next != null) Add(next, Libertadores, $"Brasileirão ({Pos(next)}º; vaga remanejada)"); }
                else Add(champion, Libertadores, "Campeão continental (vaga adicional)");
            }

        // 4. Copa do Brasil runner-up: Pré-Libertadores, or the group stage when the champion was already qualified.
        bool runnerPlaced = false;
        if (copaRunnerUp != "" && eligible(copaRunnerUp) && !used.Contains(copaRunnerUp) && (championAlreadyIn || rules.LeaguePre > 0))
            runnerPlaced = championAlreadyIn ? Add(copaRunnerUp, Libertadores, "Copa do Brasil (vice; vaga do campeão já classificado)") : Add(copaRunnerUp, Pre, "Copa do Brasil (vice)");

        // 5. Pré-Libertadores by league position; the runner-up's place goes to the table when the runner-up is not in the Pré.
        int preSlots = rules.LeaguePre == 0 ? 0 : rules.LeaguePre + (runnerPlaced && !championAlreadyIn ? 0 : 1);
        for (int i = 0; i < preSlots; i++)
        {
            var next = NextFree(); if (next == null) break;
            Add(next, Pre, $"Brasileirão ({Pos(next)}º)");
        }

        // 6. Sul-Americana: the best clubs of the table without a place.
        for (int i = 0; i < rules.SulAmericana; i++)
        {
            var next = NextFree(); if (next == null) break;
            Add(next, Sula, $"Brasileirão ({Pos(next)}º)");
        }
        return berths;
    }

    // Final table (best first) of the career league and the clubs that stay in the first division (all but the relegated).
    static (List<string> Table, HashSet<string> Relegated) FinalTable(GameState g, IReadOnlyList<SeasonStanding> standings)
    {
        var table = standings.Select(s => s.ClubId).ToList();
        return (table, table.Skip(Math.Max(0, table.Count - 4)).ToHashSet());
    }

    static string Runner(ClubCompetition? copa)
    {
        if (copa == null || copa.Champion == "") return "";
        var final = copa.Matches.Where(m => m.Stage == copa.Stage && m.Leg == 1).ToList();
        return final.Count == 1 ? (final[0].Fixture.Home == copa.Champion ? final[0].Fixture.Away : final[0].Fixture.Home) : "";
    }

    // At the end of the season of a Série A career: resolves the places of the next season, records the origin of each one,
    // and notifies the manager.
    public static void CloseSeason(GameState g, IReadOnlyList<SeasonStanding> standings)
    {
        if (g.Life == null || SeriesMovement.SeriesOf(g) != "A") { g.Qualification = null; return; }
        var (table, relegated) = FinalTable(g, standings);
        var competitions = g.Life.Competitions;
        var copa = competitions.FirstOrDefault(c => c.Id == "copa");
        var champions = new[] { competitions.FirstOrDefault(c => c.Id == Libertadores)?.Champion ?? "", competitions.FirstOrDefault(c => c.Id == Sula)?.Champion ?? "" };
        var berths = Resolve(table, c => !relegated.Contains(c), copa?.Champion ?? "", Runner(copa), champions, RulesFor(g.Season + 1));
        var state = g.Qualification ?? new QualificationState();
        state.History.Add(new(g.Season, state.Berths)); // the places this season was played with
        state.Season = g.Season + 1; state.Berths = berths; state.Clinched.Clear();
        g.Qualification = state;
        Announce(g, berths, "para a próxima temporada");
    }

    static string Label(string competition) => competition switch { Libertadores => "a fase de grupos da Libertadores", Pre => "a Pré-Libertadores", _ => "a Sul-Americana" };

    static void Announce(GameState g, List<Berth> berths, string when)
    {
        var mine = berths.FirstOrDefault(b => b.ClubId == g.ClubId);
        string text = mine == null ? $"O clube não tem vaga em competição internacional {when}." : $"O clube garantiu {Label(mine.Competition)} {when}: {mine.Origin}.";
        g.News.Insert(0, text);
        g.Life?.Press.Insert(0, new() { Season = g.Season, Round = g.Round, Kind = "Diretoria", Title = mine == null ? "Sem vaga internacional" : "Vaga internacional garantida", Text = text });
        if (mine != null && g.Life != null) g.Life.Board.Confidence = Math.Min(100, g.Life.Board.Confidence + (mine.Competition == Libertadores ? 6 : 3));
        foreach (var b in berths.Where(b => b.ClubId != g.ClubId && b.Origin.Contains("remanejada")))
            g.News.Insert(0, $"Vaga aberta pelo título de outro clube: {g.Clubs.FirstOrDefault(c => c.Id == b.ClubId)?.Name ?? b.ClubId} herda {Label(b.Competition)}.");
    }

    // First season of a career (no previous results): the places follow club strength, with a seeded draw among equals.
    public static QualificationState Seed(GameState g)
    {
        var table = g.Clubs.OrderByDescending(c => c.Strength).ThenBy(c => ClubLife.Seed(g, "entry:" + g.Season + ":" + c.Id)).Select(c => c.Id).ToList();
        var state = new QualificationState { Season = g.Season, Berths = Resolve(table, _ => true, "", "", [], RulesFor(g.Season)) };
        return state;
    }

    // ---- Table zones (projection of the places with the current table and the competitions decided so far) ----
    public static object Projection(GameState g)
    {
        if (g.Life == null || SeriesMovement.SeriesOf(g) != "A") return new { zones = new Dictionary<string, object>(), berths = Array.Empty<Berth>(), history = Array.Empty<SeasonBerths>() };
        var rows = LeagueRules.Table(g.Clubs, g.Fixtures, g.LotterySeed + ":" + g.Season).Select(r => r.Club.Id).ToList();
        var competitions = g.Life.Competitions;
        var copa = competitions.FirstOrDefault(c => c.Id == "copa");
        var champions = new[] { competitions.FirstOrDefault(c => c.Id == Libertadores)?.Champion ?? "", competitions.FirstOrDefault(c => c.Id == Sula)?.Champion ?? "" };
        var relegated = rows.Skip(Math.Max(0, rows.Count - 4)).ToHashSet();
        var berths = Resolve(rows, c => !relegated.Contains(c), copa?.Champion ?? "", Runner(copa), champions, RulesFor(g.Season + 1));
        var zones = new Dictionary<string, object>();
        foreach (var club in rows)
        {
            var b = berths.FirstOrDefault(x => x.ClubId == club);
            string zone = b != null ? b.Competition : relegated.Contains(club) ? "relegation" : "stay";
            // A club that earned the place in another competition shows where the place came from.
            bool other = b != null && !b.Origin.StartsWith("Brasileirão", StringComparison.Ordinal);
            zones[club] = new { zone, origin = b?.Origin ?? "", other };
        }
        return new { zones, berths = g.Qualification?.Berths ?? [], history = g.Qualification?.History ?? [] };
    }

    // Mathematical clinch: how many other clubs can still reach the club's points. Called after every round.
    public static void AfterRound(GameState g)
    {
        if (g.Life == null || g.Qualification == null || SeriesMovement.SeriesOf(g) != "A" || g.Fixtures.All(f => f.HomeGoals == null)) return;
        var table = LeagueRules.Table(g.Clubs, g.Fixtures, g.LotterySeed + ":" + g.Season);
        var mine = table.FirstOrDefault(r => r.Club.Id == g.ClubId); if (mine == null) return;
        int rounds = g.Fixtures.Max(f => f.Round);
        foreach (var (limit, label, key) in new[] { (4, "uma vaga direta na Libertadores (G4)", "g4"), (5, "pelo menos a Pré-Libertadores (G5)", "g5"), (11, "pelo menos a Sul-Americana (G11)", "g11") })
        {
            if (g.Qualification.Clinched.Contains(key)) continue;
            int dangerous = table.Count(r => r.Club.Id != g.ClubId && r.Points + 3 * Math.Max(0, 38 - r.Played) > mine.Points);
            if (dangerous > limit - 1 || rounds < 1) continue;
            g.Qualification.Clinched.Add(key);
            string text = $"Matematicamente garantido: o clube já assegurou {label}.";
            g.News.Insert(0, text);
            g.Life.Press.Insert(0, new() { Season = g.Season, Round = g.Round, Kind = "Diretoria", Title = "Classificação garantida", Text = text });
        }
    }
}

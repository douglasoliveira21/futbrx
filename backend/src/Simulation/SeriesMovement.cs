using System.Text.Json;
namespace Brasileirao;

// Promotion and relegation between Séries A to D. Only the managed club changes division: it takes the place of
// one club of the other league (the weakest one when it goes up, the strongest when it goes down), and the
// career continues in that league from the next season, with new squads for the new opponents.
public static class SeriesMovement
{
    // Last places that go down, by series (A and B: four; C: two, as in the 2026 edition). Série D has no relegation.
    static readonly Dictionary<string, int> Relegated = new() { ["A"] = 4, ["B"] = 4, ["C"] = 2 };
    static readonly string[] Order = ["A", "B", "C", "D"];

    sealed record Row(string Id, string Uf);
    sealed record Source(List<Row> Clubs);
    static readonly Lazy<Dictionary<string, string>> states = new(() =>
    {
        using var stream = typeof(Game).Assembly.GetManifestResourceStream("Brasileirao.Series2026.json")!;
        var source = JsonSerializer.Deserialize<Source>(stream, Game.Json)!;
        return source.Clubs.ToDictionary(c => c.Id, c => c.Uf);
    });

    // The series the career plays in, or null for demonstration and imported data versions (they have no ladder).
    public static string? SeriesOf(GameState g) => g.Divisions != null ? null : g.SerieD != null ? "D" : Game.SeriesOfVersion(g.DataVersion);

    // Series the club goes to after the season (null: it stays) from its final position (1-based) in the league.
    public static string? Destination(GameState g, string series, int position)
    {
        int i = Array.IndexOf(Order, series);
        if (series == "D") return g.SerieD!.Promoted.Contains(g.ClubId) ? "C" : null;
        if (series != "A" && SeriesFormats.EarnedPromotion(g, series, position)) return Order[i - 1];
        if (position > 20 - Relegated[series]) return Order[i + 1];
        return null;
    }

    public static void Apply(GameState g, IReadOnlyList<SeasonStanding> standings)
    {
        var from = SeriesOf(g);
        if (from == null || g.Clubs.Count == 0) return;
        int position = standings.ToList().FindIndex(r => r.ClubId == g.ClubId) + 1;
        var to = Destination(g, from, position);
        if (to == null) return;
        Move(g, from, to);
    }

    static SeasonCatalog Catalog(string series, int group = 0) => series switch
    {
        "A" => Game.Clubs2026Catalog(),
        "D" => SerieD.Catalog(group),
        _ => Game.SeriesCatalog(series)
    };
    // The Série D group closest to the club: most clubs of its state, otherwise the first group.
    static int GroupFor(Club me)
    {
        int own = SerieD.GroupOf(me.Id);
        if (own > 0) return own; // a Série D club returning to its own group
        if (!states.Value.TryGetValue(me.Id, out var uf)) return 1;
        return Enumerable.Range(1, SerieD.GroupCount).OrderByDescending(group => SerieD.Catalog(group).Clubs.Count(c => states.Value.GetValueOrDefault(c.Id) == uf)).ThenBy(group => group).First();
    }

    static void Move(GameState g, string from, string to)
    {
        bool up = Array.IndexOf(Order, to) < Array.IndexOf(Order, from);
        var me = g.Clubs.First(c => c.Id == g.ClubId);
        int group = to == "D" ? GroupFor(me) : 0;
        var catalog = Catalog(to, group);
        // Going up takes the place of the weakest club of the new league; going down, the strongest. A club that
        // returns to a league it came from simply takes its own place back.
        bool returning = catalog.Clubs.Any(c => c.Id == me.Id);
        var replaced = returning ? catalog.Clubs.First(c => c.Id == me.Id) : (up ? catalog.Clubs.OrderBy(c => c.Strength) : catalog.Clubs.OrderByDescending(c => c.Strength)).ThenBy(c => c.Id, StringComparer.Ordinal).First();
        var clubs = catalog.Clubs.Where(c => c.Id != replaced.Id && c.Id != me.Id).Append(me).ToList();
        if (clubs.Count != catalog.Clubs.Count) throw new GameRuleException("A nova divisão não pôde ser formada.");

        // Squads of the old league (and of clubs only known by name) are dropped.
        var old = g.Clubs.Select(c => c.Id).Concat(g.SerieD?.Others.Select(c => c.Id) ?? []).ToHashSet();
        foreach (var id in old.Where(id => id != g.ClubId && g.OpponentSquads.ContainsKey(id)).ToList())
        {
            foreach (var p in g.OpponentSquads[id]) g.Contracts.Remove(p.Id);
            g.OpponentSquads.Remove(id); g.ClubBalances.Remove(id);
        }
        int offset = Math.Max(900_000, g.Squad.Concat(g.Market).Concat(g.Academy).Concat(g.OpponentSquads.Values.SelectMany(s => s)).Max(p => p.Id) + 100);
        foreach (var club in clubs.Where(c => c.Id != g.ClubId))
        {
            var squad = Game.Players(offset, club.Strength); offset += 100;
            Sports.RepairSelection(squad, false);
            g.OpponentSquads[club.Id] = squad; g.ClubBalances[club.Id] = (long)(30_000_000 * Game.EconomyFactor(catalog.Version));
            foreach (var p in squad) g.Contracts[p.Id] = new() { PlayerId = p.Id, ClubId = club.Id, StartSeason = g.Season + 1, StartRound = 1, EndSeason = g.Season + 4, Wage = p.Wage };
        }
        g.Clubs = clubs; g.DataVersion = catalog.Version;
        SerieD.PrepareSquads(g);
        // A club that has just come up has no international place for the next season.
        g.Qualification = to == "A" ? new QualificationState { Season = g.Season + 1 } : null;
        g.SerieD = to == "D" ? SerieD.ForGroup(group) : null;
        if (g.SerieD != null) g.SerieD.Others.RemoveAll(c => c.Id == replaced.Id || c.Id == g.ClubId);
        g.SeasonRounds = SeriesFormats.SeasonRoundsFor(catalog.Version); g.LeagueStart = 1;
        var life = g.Life!;
        life.Fans.RivalId = clubs.Where(c => c.Id != g.ClubId).OrderBy(c => ClubLife.Seed(g, "rival:" + g.ClubId + ":" + c.Id)).First().Id;
        life.Board.TargetPosition = ClubLife.BoardTarget(g);
        string name(string series) => $"Série {series}";
        g.History.Add(returning ? $"Temporada {g.Season}: o clube {(up ? "sobe" : "cai")} e volta à {name(to)}." : $"Temporada {g.Season}: o clube {(up ? "sobe" : "cai")} para a {name(to)}, no lugar do {replaced.Name}.");
        string leaves = returning ? "" : $" ({replaced.Name} deixa a divisão)";
        g.News.Insert(0, up
            ? $"Acesso confirmado: na próxima temporada o clube disputa a {name(to)}{leaves}."
            : $"Rebaixamento: na próxima temporada o clube disputa a {name(to)}{leaves}.");
        if (!up) life.Board.Confidence = Math.Max(0, life.Board.Confidence - 20);
    }
}

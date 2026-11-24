using System.Text.Json;
namespace Brasileirao;

// The other nine South American leagues (Argentina, Uruguay, Paraguay, Chile, Colombia, Ecuador, Peru, Bolivia and Venezuela):
// real clubs (2026 editions), the format of each national championship simulated from club strength, and the clubs each country
// sends to the Libertadores and the Sudamericana. Nothing here has squads: only names, strengths and results.
public static class SouthAmerica
{
    public sealed record Row(string Id, string Name, string Short, string City, string Color, int Strength);
    public sealed record CountryData(string Code, string Name, string Format, List<Row> Clubs);
    sealed record Source(List<CountryData> Countries);

    static readonly Lazy<List<CountryData>> countries = new(() =>
    {
        using var stream = typeof(Game).Assembly.GetManifestResourceStream("Brasileirao.SouthAmerica2026.json") ?? throw new InvalidOperationException("Os clubes sul-americanos não foram incluídos na compilação.");
        return (JsonSerializer.Deserialize<Source>(stream, Game.Json) ?? throw new InvalidOperationException("Lista de clubes sul-americanos vazia.")).Countries;
    });
    public static IReadOnlyList<CountryData> Countries => countries.Value;
    public static Club ToClub(Row r) => new(r.Id, r.Name, r.Short, r.Color, r.Strength);
    public static CountryData? CountryOf(string clubId) => Countries.FirstOrDefault(c => c.Clubs.Any(x => x.Id == clubId));
    public static Club? Find(string clubId) => Countries.SelectMany(c => c.Clubs).Where(r => r.Id == clubId).Select(ToClub).FirstOrDefault();

    // Libertadores / Sudamericana places per country (the 2027 allocation): Argentina 6 and 6, the others 4 and 4.
    public static (int Libertadores, int Sudamericana) Slots(string code) => code == "ARG" ? (6, 6) : (4, 4);

    // ---- Simulation helpers ----
    sealed class Line
    {
        public string Id = ""; public int Points, Played, Wins, Draws, Losses, Goals, Against;
        public int Difference => Goals - Against;
    }
    sealed class Table
    {
        public readonly Dictionary<string, Line> Lines = [];
        public Table(IEnumerable<Club> clubs) { foreach (var c in clubs) Lines[c.Id] = new Line { Id = c.Id }; }
        public Table(Dictionary<string, Line> lines) { Lines = lines; }
    }
    static List<SeasonStanding> Standings(IEnumerable<Line> lines) => lines.Select(l => new SeasonStanding(l.Id, l.Points, l.Played, l.Wins, l.Draws, l.Losses, l.Goals, l.Against, l.Difference)).ToList();

    sealed class Context(GameState g, CountryData country, int season, Dictionary<string, Club> clubs)
    {
        public GameState G = g; public CountryData Country = country; public int Season = season; public Dictionary<string, Club> Clubs = clubs;
        public string Key(string stage) => $"sa:{Season}:{Country.Code}:{stage}";
        public Club Club(string id) => Clubs[id];
    }

    static (int Home, int Away) Match(Context x, string key, string home, string away)
        => SerieD.Score(x.Club(home).Strength, x.Club(away).Strength, ClubLife.Seed(x.G, $"{key}:{home}:{away}"));

    static void Record(Dictionary<string, Line> table, string home, string away, int h, int a)
    {
        void Apply(string id, int scored, int against) { var l = table[id]; l.Played++; l.Goals += scored; l.Against += against; if (scored > against) { l.Points += 3; l.Wins++; } else if (scored == against) { l.Points++; l.Draws++; } else l.Losses++; }
        Apply(home, h, a); Apply(away, a, h);
    }
    static List<Line> Order(Dictionary<string, Line> table, Context x) => table.Values.OrderByDescending(l => l.Points).ThenByDescending(l => l.Difference).ThenByDescending(l => l.Goals)
        .ThenByDescending(l => x.Club(l.Id).Strength).ThenBy(l => l.Id, StringComparer.Ordinal).ToList();

    // Every club plays every other one once (or home and away).
    static Dictionary<string, Line> RoundRobin(Context x, string stage, IEnumerable<string> ids, bool doubleRound)
    {
        var list = ids.ToList(); var table = list.ToDictionary(id => id, id => new Line { Id = id });
        for (int i = 0; i < list.Count; i++)
            for (int j = 0; j < list.Count; j++)
            {
                if (i == j || (!doubleRound && i > j)) continue;
                var (h, a) = Match(x, x.Key(stage), list[i], list[j]); Record(table, list[i], list[j], h, a);
            }
        return table;
    }
    // One tie (single match or home and away): aggregate goals, then penalties by a seeded draw weighted by strength.
    static string Tie(Context x, string stage, string a, string b, bool twoLegs)
    {
        var (h1, a1) = Match(x, x.Key(stage), a, b); int goalsA = h1, goalsB = a1;
        if (twoLegs) { var (h2, a2) = Match(x, x.Key(stage + "-v"), b, a); goalsA += a2; goalsB += h2; }
        if (goalsA != goalsB) return goalsA > goalsB ? a : b;
        var rng = new Random(ClubLife.Seed(x.G, x.Key(stage + "-pen") + a + b));
        double chance = .5 + (x.Club(a).Strength - x.Club(b).Strength) / 400.0;
        return rng.NextDouble() < chance ? a : b;
    }
    // Knockout over a bracket order (adjacent clubs meet, winners meet next).
    static string Knockout(Context x, string stage, List<string> bracket, bool twoLegs)
    {
        var round = bracket.ToList(); int index = 0;
        while (round.Count > 1)
        {
            var next = new List<string>();
            for (int i = 0; i + 1 < round.Count; i += 2) next.Add(Tie(x, $"{stage}-{index}-{i}", round[i], round[i + 1], twoLegs));
            if (round.Count % 2 == 1) next.Add(round[^1]);
            round = next; index++;
        }
        return round[0];
    }
    // Best against worst: 1 v 8, 4 v 5, 2 v 7, 3 v 6.
    static List<string> Bracket(List<string> seeds)
    {
        var order = new List<int> { 1 };
        while (order.Count < seeds.Count) order = order.SelectMany(s => new[] { s, order.Count * 2 + 1 - s }).ToList();
        return order.Select(s => seeds[s - 1]).ToList();
    }
    static void Add(Dictionary<string, Line> total, Dictionary<string, Line> part)
    {
        foreach (var (id, l) in part)
        {
            if (!total.TryGetValue(id, out var t)) total[id] = t = new Line { Id = id };
            t.Points += l.Points; t.Played += l.Played; t.Wins += l.Wins; t.Draws += l.Draws; t.Losses += l.Losses; t.Goals += l.Goals; t.Against += l.Against;
        }
    }
    static TournamentResult Tournament(Context x, string name, string champion, Dictionary<string, Line> table) => new(name, champion, Standings(Order(table, x)));
    static List<string> Top(Dictionary<string, Line> table, Context x, int count) => Order(table, x).Take(count).Select(l => l.Id).ToList();
    static Dictionary<string, Line> Copy(Dictionary<string, Line> t) => t.ToDictionary(p => p.Key, p => new Line { Id = p.Key, Points = p.Value.Points, Played = p.Value.Played, Wins = p.Value.Wins, Draws = p.Value.Draws, Losses = p.Value.Losses, Goals = p.Value.Goals, Against = p.Value.Against });

    // Champions play the title; the table decides the other places.
    static CountryResult Finish(Context x, List<TournamentResult> tournaments, Dictionary<string, Line> aggregate, List<string> champions, string champion, List<string>? orderOverride = null)
    {
        var order = orderOverride ?? Order(aggregate, x).Select(l => l.Id).ToList();
        var ranking = champions.Concat(order).Distinct().ToList();
        var (lib, sula) = Slots(x.Country.Code);
        return new CountryResult
        {
            Code = x.Country.Code, Season = x.Season, Tournaments = tournaments, Champion = champion, Table = Standings(Order(aggregate, x)),
            Libertadores = ranking.Take(lib).ToList(), Sudamericana = ranking.Skip(lib).Take(sula).ToList()
        };
    }

    // The format of each national championship, simplified where the source did not detail it (see docs/SERIES-AND-STATES.md).
    public static CountryResult Simulate(GameState g, CountryData country, int season)
    {
        var clubs = country.Clubs.ToDictionary(r => r.Id, ToClub);
        var x = new Context(g, country, season, clubs); var ids = country.Clubs.Select(r => r.Id).ToList();
        var tournaments = new List<TournamentResult>(); var aggregate = new Dictionary<string, Line>();
        switch (country.Code)
        {
            case "ARG":
            {
                // Apertura and Clausura: two zones of 15 (single round), the best eight of each zone play a single-match knockout.
                var champions = new List<string>();
                foreach (var name in new[] { "Apertura", "Clausura" })
                {
                    var shuffled = ids.OrderBy(id => ClubLife.Seed(g, x.Key(name + "-zone") + id)).ToList();
                    var zoneA = shuffled.Take(15).ToList(); var zoneB = shuffled.Skip(15).ToList();
                    var tA = RoundRobin(x, name + "-A", zoneA, false); var tB = RoundRobin(x, name + "-B", zoneB, false);
                    var a8 = Top(tA, x, 8); var b8 = Top(tB, x, 8);
                    var bracket = Enumerable.Range(0, 8).SelectMany(i => new[] { a8[i], b8[7 - i] }).ToList();
                    var champion = Knockout(x, name + "-ko", bracket, false); champions.Add(champion);
                    var both = Copy(tA); Add(both, tB); tournaments.Add(Tournament(x, name, champion, both)); Add(aggregate, both);
                }
                return Finish(x, tournaments, aggregate, champions, champions[1]);
            }
            case "URU":
            {
                foreach (var name in new[] { "Apertura", "Intermedio", "Clausura" })
                {
                    var t = RoundRobin(x, name, ids, false); tournaments.Add(Tournament(x, name, Top(t, x, 1)[0], t)); Add(aggregate, t);
                }
                var best = Top(aggregate, x, 2); var champion = Tie(x, "final", best[0], best[1], true);
                return Finish(x, tournaments, aggregate, [champion], champion);
            }
            case "PAR":
            {
                var champions = new List<string>();
                foreach (var name in new[] { "Apertura", "Clausura" })
                {
                    var t = RoundRobin(x, name, ids, true); var champion = Top(t, x, 1)[0]; champions.Add(champion); tournaments.Add(Tournament(x, name, champion, t)); Add(aggregate, t);
                }
                return Finish(x, tournaments, aggregate, champions, Top(aggregate, x, 1)[0]);
            }
            case "CHI":
            case "BOL":
            {
                var t = RoundRobin(x, "Liga", ids, true); var champion = Top(t, x, 1)[0];
                tournaments.Add(Tournament(x, "Campeonato", champion, t)); return Finish(x, tournaments, Copy(t), [champion], champion);
            }
            case "COL":
            {
                // Apertura: single round, best eight in a home-and-away knockout. Finalización: single round, two groups of four, final.
                var a = RoundRobin(x, "Apertura", ids, false); var a8 = Top(a, x, 8); var champA = Knockout(x, "Apertura-ko", Bracket(a8), true);
                var f = RoundRobin(x, "Finalizacion", ids.AsEnumerable().Reverse(), false); var f8 = Top(f, x, 8);
                var g1 = new[] { f8[0], f8[3], f8[4], f8[7] }; var g2 = new[] { f8[1], f8[2], f8[5], f8[6] };
                var w1 = Top(RoundRobin(x, "Finalizacion-G1", g1, true), x, 1)[0]; var w2 = Top(RoundRobin(x, "Finalizacion-G2", g2, true), x, 1)[0];
                var champF = Tie(x, "Finalizacion-final", w1, w2, true);
                tournaments.Add(Tournament(x, "Apertura", champA, a)); tournaments.Add(Tournament(x, "Finalización", champF, f)); Add(aggregate, a); Add(aggregate, f);
                return Finish(x, tournaments, aggregate, [champA, champF], champF);
            }
            case "ECU":
            {
                // Two rounds, then a hexagonal of the champions' group with the points carried over.
                var first = RoundRobin(x, "Primera", ids, true); var six = Top(first, x, 6);
                var hex = Copy(first).Where(p => six.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value);
                Add(hex, RoundRobin(x, "Hexagonal", six, true));
                var order = Order(hex, x).Select(l => l.Id).Concat(Order(first, x).Select(l => l.Id)).Distinct().ToList();
                var champion = order[0];
                tournaments.Add(Tournament(x, "Primeira fase", Top(first, x, 1)[0], first)); tournaments.Add(Tournament(x, "Hexagonal final", champion, hex));
                return Finish(x, tournaments, Copy(first), [champion], champion, order);
            }
            case "PER":
            {
                var champions = new List<string>();
                foreach (var name in new[] { "Apertura", "Clausura" })
                {
                    var t = RoundRobin(x, name, ids, false); var champion = Top(t, x, 1)[0]; champions.Add(champion); tournaments.Add(Tournament(x, name, champion, t)); Add(aggregate, t);
                }
                // Playoffs: the two winners and the two best of the aggregate table.
                var four = champions.Concat(Top(aggregate, x, 6)).Distinct().Take(4).ToList();
                var champ = Knockout(x, "Playoffs", Bracket(four.OrderByDescending(id => aggregate[id].Points).ToList()), true);
                return Finish(x, tournaments, aggregate, [champ, .. champions], champ);
            }
            case "VEN":
            {
                var champions = new List<string>();
                foreach (var name in new[] { "Apertura", "Clausura" })
                {
                    var t = RoundRobin(x, name, ids, false); var e8 = Top(t, x, 8);
                    var w1 = Top(RoundRobin(x, name + "-G1", new[] { e8[0], e8[3], e8[4], e8[7] }, false), x, 1)[0]; var w2 = Top(RoundRobin(x, name + "-G2", new[] { e8[1], e8[2], e8[5], e8[6] }, false), x, 1)[0];
                    var champion = Tie(x, name + "-final", w1, w2, false); champions.Add(champion); tournaments.Add(Tournament(x, name, champion, t)); Add(aggregate, t);
                }
                var title = champions[0] == champions[1] ? champions[0] : Tie(x, "Final-do-ano", champions[0], champions[1], true);
                return Finish(x, tournaments, aggregate, [title, .. champions], title);
            }
            default: throw new InvalidOperationException("País sul-americano desconhecido: " + country.Code);
        }
    }

    public static List<CountryResult> SimulateAll(GameState g, int season) => Countries.Select(c => Simulate(g, c, season)).ToList();

    // Results that qualify clubs for the continental cups of the season of the career: those of the season before.
    public static SouthAmericaState Ensure(GameState g)
    {
        if (g.SouthAmerica == null || g.SouthAmerica.Season != g.Season) g.SouthAmerica = new SouthAmericaState { Season = g.Season, Results = SimulateAll(g, g.Season - 1) };
        return g.SouthAmerica;
    }
    // At the end of a season: the championships of this season qualify clubs for the next one.
    public static void CloseSeason(GameState g) => g.SouthAmerica = new SouthAmericaState { Season = g.Season + 1, Results = SimulateAll(g, g.Season) };
}

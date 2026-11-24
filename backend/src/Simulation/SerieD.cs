using System.Text.Json;
namespace Brasileirao;

// Série D 2026: 96 clubs in 16 groups of six, ten rounds of home and away matches, the best four of every group
// go to two-legged knockouts down to the final, and six clubs go up. The user plays the real group of the chosen
// club; the other fifteen groups are simulated in the background from club strength, keeping only the standings,
// and the squads of clubs the user never meets are never created.
public static class SerieD
{
    public const int GroupRounds = 10;
    public const int SeasonRounds = 22;
    // Two-legged stages: 64 clubs, 32, 16, 8 (quartas), 4 (semifinais) and the final.
    public static readonly int[] KnockoutRounds = [11, 13, 15, 17, 19, 21];
    public const int PlayoffRound = 19;
    public static readonly string[] StageNames = ["Primeira fase", "Segunda fase", "Oitavas de final", "Quartas de final", "Semifinais", "Final"];

    sealed record Row(string Id, string Series, string Name, string Short, string Color, int Strength, string Uf, string City, int Group = 0);
    sealed record Source(List<Row> Clubs);
    // Geographic order (north to south, west to east) used to form groups of neighbouring states.
    static readonly string[] UfOrder = ["AC", "RO", "AM", "RR", "AP", "PA", "TO", "MA", "PI", "CE", "RN", "PB", "PE", "AL", "SE", "BA", "MT", "MS", "GO", "DF", "MG", "ES", "RJ", "SP", "PR", "SC", "RS"];

    // The 16 groups, 6 clubs each: the official groups A1 to A16 when the directory has them (field group), otherwise
    // groups formed by geography.
    static readonly Lazy<List<List<Row>>> groups = new(() =>
    {
        using var stream = typeof(Game).Assembly.GetManifestResourceStream("Brasileirao.Series2026.json")
            ?? throw new InvalidOperationException("O diretório de clubes por série não foi incluído na compilação.");
        var source = JsonSerializer.Deserialize<Source>(stream, Game.Json) ?? throw new InvalidOperationException("O diretório de clubes por série está vazio.");
        var ordered = source.Clubs.Where(c => c.Series == "D")
            .OrderBy(c => Math.Max(0, Array.IndexOf(UfOrder, c.Uf))).ThenBy(c => c.City, StringComparer.Ordinal).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        if (ordered.Count != 96) throw new InvalidOperationException($"A Série D precisa de 96 clubes no diretório (encontrados {ordered.Count}).");
        var official = ordered.GroupBy(c => c.Group).Where(g => g.Key is >= 1 and <= 16).OrderBy(g => g.Key).Select(g => g.OrderBy(c => c.Id, StringComparer.Ordinal).ToList()).ToList();
        if (official.Count == 16 && official.All(g => g.Count == 6)) return official;
        return ordered.Chunk(6).Select(chunk => chunk.ToList()).ToList();
    });

    public static int GroupCount => groups.Value.Count;
    public static string Version(int group) => $"{Game.SerieDVersionPrefix}{group:00}-v1";
    public static IEnumerable<string> Versions() => Enumerable.Range(1, GroupCount).Select(Version);
    public static int GroupOf(string clubId)
    {
        for (int i = 0; i < groups.Value.Count; i++) if (groups.Value[i].Any(c => c.Id == clubId)) return i + 1;
        return 0;
    }
    public static int GroupOfVersion(string version) =>
        version.StartsWith(Game.SerieDVersionPrefix, StringComparison.Ordinal) && int.TryParse(version.AsSpan(Game.SerieDVersionPrefix.Length, 2), out var n) && n >= 1 && n <= GroupCount ? n : 0;
    public static bool IsVersion(string version) => GroupOfVersion(version) > 0;

    // One data version per group: the six clubs of the group, simulated squads and a few free agents.
    public static SeasonCatalog Catalog(int group)
    {
        var clubs = groups.Value[group - 1].Select(c => new Club(c.Id, c.Name, c.Short, c.Color, c.Strength)).ToList();
        return new SeasonCatalog
        {
            Version = Version(group),
            Season = 2026,
            Source = $"Grupo {group} da Série D de 2026, conforme as tabelas da página da edição na Wikipédia (fatos públicos), lidas em 23/11/2026. Siglas, cores e forças são estimativas do projeto, ainda a conferir.",
            License = "Elencos, agentes livres e atributos simulados pelo futbrx. Escudos genéricos, sem imagens oficiais.",
            Clubs = clubs,
            Players = clubs.SelectMany((club, index) => Game.Players(index * 100).Select(player => new CatalogPlayer(club.Id, player)))
                .Concat(Game.Players(10000).Take(12).Select(player => new CatalogPlayer("", player))).ToList()
        };
    }
    // Directory fields of the Série D clubs: group number and the data version that starts a career there.
    public static (int Group, string Version) Entry(string clubId) { int g = GroupOf(clubId); return (g, g == 0 ? "" : Version(g)); }

    // State of a new Série D career: the group is the league, the other clubs are names and strengths.
    public static SerieDState Create(SeasonCatalog catalog) => ForGroup(GroupOfVersion(catalog.Version));
    public static SerieDState ForGroup(int group)
    {
        var state = new SerieDState { Group = group };
        for (int i = 0; i < groups.Value.Count; i++)
            foreach (var c in groups.Value[i])
            {
                state.Groups[c.Id] = i + 1;
                if (i + 1 != group) state.Others.Add(new Club(c.Id, c.Name, c.Short, c.Color, c.Strength));
            }
        return state;
    }
    public static void NewSeason(GameState g)
    {
        if (g.SerieD == null) return;
        g.SerieD.Results.Clear(); g.SerieD.Qualified.Clear(); g.SerieD.Promoted.Clear();
    }

    public static IEnumerable<Club> AllClubs(GameState g) => g.Clubs.Concat(g.SerieD?.Others ?? []);
    public static int Strength(GameState g, string id) => AllClubs(g).FirstOrDefault(c => c.Id == id)?.Strength ?? 50;

    // A club the user meets for the first time gets its squad now, so the save only holds squads that were needed.
    public static void EnsureSquad(GameState g, string clubId)
    {
        var club = g.SerieD?.Others.Find(c => c.Id == clubId);
        if (club == null || clubId == g.ClubId || g.OpponentSquads.ContainsKey(clubId)) return;
        int offset = Math.Max(800_000, g.Squad.Concat(g.Market).Concat(g.Academy).Concat(g.OpponentSquads.Values.SelectMany(s => s)).Max(p => p.Id) + 100);
        var squad = Game.Players(offset); g.OpponentSquads[clubId] = squad; g.ClubBalances.TryAdd(clubId, 5_000_000);
        foreach (var p in squad) g.Contracts[p.Id] = new() { PlayerId = p.Id, ClubId = clubId, StartSeason = g.Season, StartRound = 1, EndSeason = g.Season + 3, Wage = p.Wage };
    }

    static int Poisson(Random rng, double mean)
    {
        double limit = Math.Exp(-mean), product = 1; int k = 0;
        do { k++; product *= rng.NextDouble(); } while (product > limit);
        return k - 1;
    }
    // Goals of a match played without squads: the stronger club scores more, with home advantage.
    static (int Home, int Away) Score(int homeStrength, int awayStrength, int seed)
    {
        var rng = new Random(seed); double edge = (homeStrength - awayStrength) / 100.0;
        return (Poisson(rng, Math.Clamp(1.35 + edge * 1.6, .35, 3.2)), Poisson(rng, Math.Clamp(1.05 - edge * 1.6, .25, 3.0)));
    }
    // A knockout match between two clubs the user does not manage.
    public static void LightPlay(GameState g, ClubCompetition competition, CupMatch match)
    {
        var f = match.Fixture; var (home, away) = Score(Strength(g, f.Home), Strength(g, f.Away), f.Seed);
        f.HomeGoals = home; f.AwayGoals = away;
        if (competition.Legs == 2 && match.Leg == 2)
        {
            var first = competition.Matches.First(m => m.Tie == match.Tie && m.Leg == 1).Fixture;
            if (home + first.AwayGoals == away + first.HomeGoals)
            {
                var rng = new Random(f.Seed ^ 0x5151); int taken = rng.Next(3, 6), other = rng.Next(0, taken + 1);
                if (other == taken) other = taken - 1;
                bool homeWins = rng.Next(2) == 0; f.HomePenalties = homeWins ? taken : other; f.AwayPenalties = homeWins ? other : taken;
            }
        }
    }

    // The group stage is over when every league match of the user's group has been played.
    public static void AfterRound(GameState g)
    {
        var state = g.SerieD;
        if (state == null || state.Qualified.Count > 0 || g.Fixtures.Count == 0 || g.Fixtures.Any(f => f.HomeGoals == null)) return;
        Start(g);
    }

    static SerieDGroupResult Simulate(GameState g, int group, List<Row> clubs)
    {
        var rows = clubs.ToDictionary(c => c.Id, c => new int[6]); // points, played, wins, draws, goals, against
        foreach (var home in clubs) foreach (var away in clubs)
        {
            if (home.Id == away.Id) continue;
            var (h, a) = Score(home.Strength, away.Strength, ClubLife.Seed(g, $"dgroup:{g.Season}:{group}:{home.Id}:{away.Id}"));
            void Apply(string id, int scored, int against) { var r = rows[id]; r[1]++; r[4] += scored; r[5] += against; if (scored > against) { r[0] += 3; r[2]++; } else if (scored == against) { r[0]++; r[3]++; } }
            Apply(home.Id, h, a); Apply(away.Id, a, h);
        }
        var table = rows.OrderByDescending(r => r.Value[0]).ThenByDescending(r => r.Value[4] - r.Value[5]).ThenByDescending(r => r.Value[4])
            .ThenByDescending(r => clubs.First(c => c.Id == r.Key).Strength).ThenBy(r => r.Key, StringComparer.Ordinal)
            .Select(r => new SeasonStanding(r.Key, r.Value[0], r.Value[1], r.Value[2], r.Value[3], r.Value[1] - r.Value[2] - r.Value[3], r.Value[4], r.Value[5], r.Value[4] - r.Value[5])).ToList();
        return new SerieDGroupResult { Group = group, Table = table };
    }

    // Standard bracket order: seed 1 meets the last seed, and the winners of neighbouring ties meet next.
    static List<int> BracketOrder(int size)
    {
        var order = new List<int> { 1 };
        while (order.Count < size) order = order.SelectMany(seed => new[] { seed, order.Count * 2 + 1 - seed }).ToList();
        return order;
    }

    static void Start(GameState g)
    {
        var state = g.SerieD!; state.Results.Clear();
        for (int i = 0; i < groups.Value.Count; i++)
        {
            int group = i + 1;
            if (group != state.Group) { state.Results.Add(Simulate(g, group, groups.Value[i])); continue; }
            var table = LeagueRules.Table(g.Clubs, g.Fixtures, g.LotterySeed + ":" + g.Season);
            state.Results.Add(new SerieDGroupResult { Group = group, Table = table.Select(r => new SeasonStanding(r.Club.Id, r.Points, r.Played, r.Wins, r.Draws, r.Losses, r.Goals, r.Against, r.Difference)).ToList() });
        }
        var qualified = state.Results.SelectMany(r => r.Table.Take(4).Select((row, position) => (Row: row, Position: position)))
            .OrderBy(x => x.Position).ThenByDescending(x => x.Row.Points).ThenByDescending(x => x.Row.Difference).ThenByDescending(x => x.Row.Goals)
            .ThenByDescending(x => Strength(g, x.Row.ClubId)).ThenBy(x => x.Row.ClubId, StringComparer.Ordinal).Select(x => x.Row.ClubId).ToList();
        state.Qualified = qualified;
        var seeds = BracketOrder(qualified.Count).Select(seed => qualified[seed - 1]).ToList();
        var competition = ClubCompetitions.Add(g, "serie-d", "Série D · fase final", seeds, [.. KnockoutRounds], 600_000, 2);
        competition.Light = true;
        competition.Format = "Os quatro primeiros de cada um dos 16 grupos (64 clubes) jogam mata-matas de ida e volta até a final. Vence o melhor agregado e, no empate, os pênaltis. Sobem seis clubes: os dois finalistas, os dois semifinalistas e os dois vencedores da repescagem entre os eliminados nas quartas.";
        int position = state.Results.First(r => r.Group == state.Group).Table.FindIndex(r => r.ClubId == g.ClubId) + 1;
        g.News.Insert(0, position <= 4
            ? $"Série D: o clube terminou o grupo em {position}º lugar e está na fase final, com 64 clubes."
            : $"Série D: o clube terminou o grupo em {position}º lugar e não avançou à fase final.");
    }

    // The quarterfinal losers play two promotion playoffs while the semifinals are played.
    public static void OnStage(GameState g, ClubCompetition competition)
    {
        if (g.SerieD == null || competition.Id != "serie-d" || competition.Stage != 4) return;
        var losers = competition.Matches.Where(m => m.Stage == 3 && m.Leg == 1).Select(m => m.Fixture.Home == m.Winner ? m.Fixture.Away : m.Fixture.Home)
            .OrderBy(id => g.SerieD.Qualified.IndexOf(id)).ToList();
        if (losers.Count != 4) return;
        for (int i = 0; i < 2; i++)
        {
            var playoff = ClubCompetitions.Add(g, $"serie-d-playoff-{i + 1}", $"Série D · repescagem do acesso {i + 1}", [losers[i], losers[3 - i]], [PlayoffRound], 200_000, 2);
            playoff.Light = true; playoff.Promotion = true;
            playoff.Format = "Repescagem entre eliminados nas quartas de final: ida e volta e o vencedor sobe para a Série C.";
        }
    }
    // The final decides the champion; with it the six promoted clubs are known.
    public static void OnChampion(GameState g, ClubCompetition competition)
    {
        if (g.SerieD == null || competition.Id != "serie-d") return;
        var final = competition.Matches.Where(m => m.Stage == 5 && m.Leg == 1).ToList();
        var semifinalLosers = competition.Matches.Where(m => m.Stage == 4 && m.Leg == 1).Select(m => m.Fixture.Home == m.Winner ? m.Fixture.Away : m.Fixture.Home);
        var playoffWinners = g.Life!.Competitions.Where(c => c.Id.StartsWith("serie-d-playoff-", StringComparison.Ordinal) && c.Champion != "").Select(c => c.Champion);
        g.SerieD.Promoted = final.SelectMany(m => new[] { m.Fixture.Home, m.Fixture.Away }).Concat(semifinalLosers).Concat(playoffWinners).Distinct().ToList();
        string Name(string id) => AllClubs(g).FirstOrDefault(c => c.Id == id)?.Name ?? id;
        g.News.Insert(0, "Série D: sobem para a Série C " + string.Join(", ", g.SerieD.Promoted.Select(Name)) + ".");
        if (g.SerieD.Promoted.Contains(g.ClubId)) { g.History.Add($"Temporada {g.Season}: acesso à Série C conquistado."); g.Life.Board.Confidence = Math.Min(100, g.Life.Board.Confidence + 15); }
    }
}

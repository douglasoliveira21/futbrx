namespace Brasileirao;

// Formats of the national series that go beyond a plain league (2026 regulations, from the pages of each edition):
//  - Série A: 38 rounds, home and away; the last four go down.
//  - Série B: 38 rounds; the first two go up, 3rd x 6th and 4th x 5th play home and away for the other two places
//    (the better placed hosts the second leg; no extra time or penalties); the last four go down.
//  - Série C: first phase in a single round (19 rounds); the best eight go on, the last two go down; the second phase
//    has two groups of four (1st, 4th, 5th, 8th and 2nd, 3rd, 6th, 7th), home and away; the two best of each group go
//    up and the group winners play the final, home and away.
//  - Série D: see SerieD.
public static class SeriesFormats
{
    public const int SerieBPlayoffRound = 39, SerieCGroupsRound = 20, SerieCFinalRound = 26;

    public static int SeasonRoundsFor(string version) =>
        version == Game.SerieC2026Version ? 27
        : version == Game.SerieB2026Version ? 40
        : SerieD.IsVersion(version) ? SerieD.SeasonRounds : 38;
    public static bool SingleRound(GameState g) => g.DataVersion == Game.SerieC2026Version;

    static List<LeagueRules.Standing> Table(GameState g) => LeagueRules.Table(g.Clubs, g.Fixtures, g.LotterySeed + ":" + g.Season).ToList();

    // Called after every round: when the league phase is over, the next phase of the series is created.
    public static void AfterRound(GameState g)
    {
        if (g.Divisions != null || g.SerieD != null || g.Life == null || g.Fixtures.Count == 0 || g.Fixtures.Any(f => f.HomeGoals == null)) return;
        var competitions = g.Life.Competitions;
        if (g.DataVersion == Game.SerieB2026Version && !competitions.Any(c => c.Id.StartsWith("serie-b-playoff-", StringComparison.Ordinal))) StartPlayoffs(g);
        else if (g.DataVersion == Game.SerieC2026Version && !competitions.Any(c => c.Id == "serie-c-groups")) StartGroups(g);
    }

    static void StartPlayoffs(GameState g)
    {
        var t = Table(g).Select(r => r.Club.Id).ToList();
        // The lower placed club hosts the first leg; the better placed one hosts the second.
        var ties = new[] { (Id: "1", Low: t[5], High: t[2], Label: "3º × 6º"), (Id: "2", Low: t[4], High: t[3], Label: "4º × 5º") };
        foreach (var tie in ties)
        {
            var playoff = ClubCompetitions.Add(g, "serie-b-playoff-" + tie.Id, $"Série B · playoff do acesso ({tie.Label})", [tie.Low, tie.High], [SerieBPlayoffRound], 0, 2, keepOrder: true, noPenalties: true);
            playoff.Promotion = true;
            playoff.Format = "Playoff de acesso à Série A: ida e volta, com a segunda partida na casa do clube mais bem colocado. Vence o melhor agregado e, no empate, o clube mais bem colocado na liga: não há prorrogação nem pênaltis.";
        }
        g.News.Insert(0, "Série B: fim da fase de pontos corridos. Os dois primeiros sobem; do 3º ao 6º, playoffs de ida e volta definem os outros dois acessos.");
    }

    static void StartGroups(GameState g)
    {
        var t = Table(g).Select(r => r.Club.Id).ToList();
        var groups = new[] { new[] { t[0], t[3], t[4], t[7] }, new[] { t[1], t[2], t[5], t[6] } };
        var competition = new ClubCompetition
        {
            Id = "serie-c-groups", Name = "Série C · segunda fase", Rounds = Enumerable.Range(SerieCGroupsRound, 6).ToList(), Prize = 0, Legs = 1, GroupStage = true,
            Format = "Os oito melhores da primeira fase formam dois grupos de quatro (1º, 4º, 5º e 8º; 2º, 3º, 6º e 7º), em ida e volta. Os dois primeiros de cada grupo sobem para a Série B e os líderes disputam a final."
        };
        int index = 0;
        for (int k = 0; k < groups.Length; k++) AddRoundRobin(g, competition, "g" + (k + 1), groups[k].ToList(), SerieCGroupsRound, true, ref index);
        g.Life!.Competitions.Add(competition);
        g.News.Insert(0, "Série C: fim da primeira fase. Os oito melhores seguem para a segunda fase, em dois grupos; os dois últimos descem.");
    }

    // Round robin by the circle method: n-1 rounds from firstRound, and the same pairs with the venues swapped when home and away.
    internal static void AddRoundRobin(GameState g, ClubCompetition competition, string tie, List<string> ids, int firstRound, bool doubleRound, ref int index)
    {
        int n = ids.Count;
        for (int half = 0; half < (doubleRound ? 2 : 1); half++)
        {
            var order = ids.ToList();
            for (int r = 0; r < n - 1; r++)
            {
                for (int i = 0; i < n / 2; i++)
                {
                    var (home, away) = (order[i], order[n - 1 - i]);
                    if ((r % 2 == 1) ^ (half == 1)) (home, away) = (away, home);
                    int round = firstRound + half * (n - 1) + r;
                    competition.Matches.Add(ClubCompetitions.Leg(g, competition, tie, $"{g.Season}-{competition.Id}-{tie}-{index}", 1, index, home, away, round));
                    index++;
                }
                var last = order[^1]; order.RemoveAt(n - 1); order.Insert(1, last);
            }
        }
    }

    // ---- State championship of the club's own state ----
    sealed record DirRow(string Id, string Name, string Short, string Color, int Strength, string Uf);
    sealed record DirSource(List<DirRow> Clubs);
    static readonly Lazy<List<DirRow>> directory = new(() =>
    {
        using var stream = typeof(Game).Assembly.GetManifestResourceStream("Brasileirao.Series2026.json")!;
        return System.Text.Json.JsonSerializer.Deserialize<DirSource>(stream, Game.Json)!.Clubs;
    });
    static readonly string[] UfOrder = ["AC", "RO", "AM", "RR", "AP", "PA", "TO", "MA", "PI", "CE", "RN", "PB", "PE", "AL", "SE", "BA", "MT", "MS", "GO", "DF", "MG", "ES", "RJ", "SP", "PR", "SC", "RS"];
    static readonly Dictionary<string, string> StateNames = new()
    {
        ["AC"] = "Acreano", ["AL"] = "Alagoano", ["AP"] = "Amapaense", ["AM"] = "Amazonense", ["BA"] = "Baiano", ["CE"] = "Cearense", ["DF"] = "Brasiliense", ["ES"] = "Capixaba",
        ["GO"] = "Goiano", ["MA"] = "Maranhense", ["MT"] = "Mato-Grossense", ["MS"] = "Sul-Mato-Grossense", ["MG"] = "Mineiro", ["PA"] = "Paraense", ["PB"] = "Paraibano", ["PR"] = "Paranaense",
        ["PE"] = "Pernambucano", ["PI"] = "Piauiense", ["RJ"] = "Carioca", ["RN"] = "Potiguar", ["RS"] = "Gaúcho", ["RO"] = "Rondoniense", ["RR"] = "Roraimense", ["SC"] = "Catarinense",
        ["SP"] = "Paulista", ["SE"] = "Sergipano", ["TO"] = "Tocantinense"
    };
    public const int StateClubs = 8, StateFirstRound = 2, StateSemifinalRound = 9, StateFinalRound = 11;
    public static string? StateOf(string clubId) => directory.Value.FirstOrDefault(c => c.Id == clubId)?.Uf;

    // The eight clubs of the state championship: the club and the strongest of its state; states with fewer clubs in the
    // directory borrow the strongest clubs of the nearest states.
    static List<Club> StateField(GameState g, string uf)
    {
        int at = Math.Max(0, Array.IndexOf(UfOrder, uf));
        var pool = directory.Value.Where(c => c.Id != g.ClubId)
            .OrderBy(c => c.Uf == uf ? 0 : 1).ThenBy(c => Math.Abs(Array.IndexOf(UfOrder, c.Uf) - at))
            .ThenByDescending(c => c.Strength).ThenBy(c => c.Id, StringComparer.Ordinal).Take(StateClubs - 1)
            .Select(c => new Club(c.Id, c.Name, c.Short, c.Color, c.Strength)).ToList();
        return [g.Clubs.First(c => c.Id == g.ClubId), .. pool];
    }

    // Replaces the demonstration circuit when the club belongs to a state of the directory: a single round robin of eight
    // clubs, then home-and-away semifinals and final among the best four. Returns false when the career has no state.
    public static bool CreateState(GameState g)
    {
        var uf = StateOf(g.ClubId);
        // Demonstration careers (fictional data) keep the generic circuit.
        if (uf == null || g.Divisions != null || g.DataVersion.StartsWith("fictional-", StringComparison.Ordinal) || !StateNames.TryGetValue(uf, out var demonym)) return false;
        var field = StateField(g, uf);
        AddKnown(g, field);
        var competition = new ClubCompetition
        {
            Id = "state", Name = "Campeonato " + demonym, Rounds = Enumerable.Range(StateFirstRound, StateClubs - 1).ToList(), Prize = StatePrizes[uf].Champion, RunnerUpPrize = StatePrizes[uf].Runner, EntryPrize = StateEntryPrize(g, uf), Legs = 1, GroupStage = true, Light = true,
            Format = $"Campeonato {demonym}: oito clubes do estado (ou dos vizinhos, quando faltam clubes na base) em turno único; os quatro primeiros jogam semifinais e final em ida e volta. Formato simplificado, sem o regulamento oficial da federação."
        };
        int index = 0;
        AddRoundRobin(g, competition, "g1", field.Select(c => c.Id).ToList(), StateFirstRound, false, ref index);
        g.Life!.Competitions.Add(competition);
        return true;
    }

    static void StartStateKnockout(GameState g, ClubCompetition groups)
    {
        var table = GroupTable(g, groups, "g1");
        groups.Advanced = table.Take(4).Select(r => r.Club.Id).ToList();
        // Lower placed hosts the first leg of each semifinal: 4th x 1st and 3rd x 2nd.
        var final = ClubCompetitions.Add(g, "state-final", groups.Name + " · fase final", [groups.Advanced[3], groups.Advanced[0], groups.Advanced[2], groups.Advanced[1]], [StateSemifinalRound, StateFinalRound], groups.Prize, 2, keepOrder: false);
        final.Light = true; final.RunnerUpPrize = groups.RunnerUpPrize;
        final.Format = "Semifinais e final em ida e volta entre os quatro primeiros do campeonato estadual. Vence o melhor agregado e, no empate, os pênaltis.";
        g.News.Insert(0, groups.Name + ": fim da primeira fase. Os quatro primeiros seguem para as semifinais.");
    }

    // Copa do Brasil 2026 cotas (CBF), accumulated by phase. The game plays the last sixteen clubs, so the club receives on its
    // first match the cotas of the phases it would already have played: Série A enters in the 5th phase, Série B in the 2nd,
    // the others in the 1st. Then: oitavas 3, quartas 4, semifinal 9 (R$ mi), runner-up 34, champion 78.
    public static void ApplyCopaPrizes(GameState g, ClubCompetition copa)
    {
        long mi = 1_000_000;
        long entry = (SeriesMovement.SeriesOf(g) ?? "A") switch
        {
            "A" => 2 * mi,
            "B" => 1_380_000 + 1_530_000 + 1_680_000 + 2 * mi,
            _ => 400_000 + 830_000 + 950_000 + 1_070_000 + 2 * mi
        };
        copa.StagePrizes = [entry + 3 * mi, 4 * mi, 9 * mi, 0];
        copa.RunnerUpPrize = 34 * mi;
        copa.Format += " Cotas da CBF para 2026, acumuladas por fase: oitavas R$ 3 mi, quartas R$ 4 mi, semifinal R$ 9 mi, vice R$ 34 mi e campeão R$ 78 mi, além das fases anteriores conforme a série.";
    }

    static void Pay(GameState g, string text, long amount) { g.Balance += amount; g.Ledger.Add(new(g.Round, text, amount, g.Season)); }
    internal static void AddKnown(GameState g, IEnumerable<Club> clubs)
    {
        foreach (var club in clubs.Where(c => g.Clubs.All(x => x.Id != c.Id) && g.KnownClubs.All(x => x.Id != c.Id) && g.SerieD?.Others.Any(o => o.Id == c.Id) != true))
            g.KnownClubs.Add(club);
    }

    // ---- Prize money (values supplied for the game: league tables are balance proposals, not official payments) ----
    const long Mi = 1_000_000;
    // Série A: by final position (R$ mi); 17th to 20th receive nothing.
    static readonly long[] SerieAPrizes = [54 * Mi, 50 * Mi, 47 * Mi, 44 * Mi, 41 * Mi, 38 * Mi, 35 * Mi, 32 * Mi, 29 * Mi, 26 * Mi, 23 * Mi, 21 * Mi, 19 * Mi, 17 * Mi, 15 * Mi, 13 * Mi];
    // Série B by position (1st to 8th). Séries C and D pay by phase (see StartGroups/OnGroupMatch and SerieD).
    static readonly long[] SerieBPrizes = [6 * Mi, 4 * Mi, 3 * Mi, 2_500_000, 1_500_000, 1 * Mi, 800_000, 600_000];

    // League prize paid at the end of the season, by position in the league table.
    public static long LeaguePrize(GameState g, int place)
    {
        var series = SeriesMovement.SeriesOf(g);
        if (series == null) return Math.Max(500_000, (21 - place) * 500_000L); // demonstration careers keep the old table
        return series switch
        {
            "A" => place >= 1 && place <= SerieAPrizes.Length ? SerieAPrizes[place - 1] : 0,
            "B" => place >= 1 && place <= SerieBPrizes.Length ? SerieBPrizes[place - 1] : 0,
            _ => 0
        };
    }

    // State championships 2026: champion and runner-up (R$).
    static readonly Dictionary<string, (long Champion, long Runner)> StatePrizes = new()
    {
        ["SP"] = (5 * Mi, 1_650_000), ["RJ"] = (10 * Mi, 5_500_000), ["MG"] = (2 * Mi, 1 * Mi), ["RS"] = (2 * Mi, 1 * Mi), ["PR"] = (800_000, 400_000), ["SC"] = (600_000, 300_000),
        ["BA"] = (500_000, 250_000), ["PE"] = (400_000, 200_000), ["GO"] = (400_000, 200_000), ["PA"] = (350_000, 175_000), ["AL"] = (250_000, 125_000), ["RN"] = (250_000, 125_000),
        ["PB"] = (200_000, 100_000), ["MA"] = (200_000, 100_000), ["AM"] = (200_000, 100_000), ["ES"] = (150_000, 75_000), ["MT"] = (150_000, 75_000), ["MS"] = (100_000, 50_000),
        ["DF"] = (150_000, 75_000), ["SE"] = (100_000, 50_000), ["PI"] = (100_000, 50_000), ["TO"] = (80_000, 40_000), ["AC"] = (60_000, 30_000), ["RO"] = (60_000, 30_000),
        ["RR"] = (50_000, 25_000), ["AP"] = (50_000, 25_000),
        // Ceará is missing from the supplied table: an estimate in line with the states of similar size.
        ["CE"] = (400_000, 200_000)
    };
    // Participation cota of the main clubs (Série A): Paulista R$ 35 mi; Carioca between R$ 6,6 and 10 mi (the midpoint is used).
    static long StateEntryPrize(GameState g, string uf) =>
        SeriesMovement.SeriesOf(g) != "A" ? 0 : uf == "SP" ? 35 * Mi : uf == "RJ" ? 8_300_000 : 0;

    static readonly string[] NortheastUf = ["BA", "SE", "AL", "PE", "PB", "RN", "CE", "PI", "MA"];
    static readonly string[] VerdeUf = ["AM", "PA", "AP", "RR", "RO", "AC", "TO", "MT", "MS", "GO", "DF", "ES"];
    // Copa do Nordeste and Copa Verde 2026 for clubs of those regions: a knockout of eight clubs, with the participation cota of
    // the national-ranking group (here: Série A = group 1 ... Série D = group 4), then cotas by phase, runner-up and champion.
    public static void CreateRegionalCups(GameState g)
    {
        var uf = StateOf(g.ClubId);
        if (uf == null || g.Divisions != null || g.DataVersion.StartsWith("fictional-", StringComparison.Ordinal)) return;
        int group = Math.Max(0, Array.IndexOf(new[] { "A", "B", "C", "D" }, SeriesMovement.SeriesOf(g) ?? "A"));
        if (NortheastUf.Contains(uf))
            Regional(g, "copa-nordeste", "Copa do Nordeste", NortheastUf, [3, 7, 11], new long[] { 2_500_000, 1_350_000, 850_000, 500_000 }[group], [500_000, 600_000, 0], 700_000, 1 * Mi,
                "Copa do Nordeste: oito clubes da região em mata-mata de jogo único. Cotas de 2026: participação por grupo do ranking nacional, quartas R$ 500 mil, semifinal R$ 600 mil, vice R$ 700 mil e campeão R$ 1 mi.");
        if (VerdeUf.Contains(uf))
            Regional(g, "copa-verde", "Copa Verde", VerdeUf, [4, 8, 12], new long[] { 200_000, 125_000, 100_000, 75_000 }[group], [100_000, 250_000, 0], 250_000, 400_000,
                "Copa Verde: oito clubes do Norte e Centro-Oeste em mata-mata de jogo único. Cotas de 2026: participação por grupo do ranking nacional, segunda fase R$ 100 mil, semifinal R$ 250 mil, vice R$ 250 mil e campeão R$ 400 mil.");
    }
    static void Regional(GameState g, string id, string name, string[] ufs, int[] rounds, long entry, long[] stages, long runnerUp, long champion, string format)
    {
        var others = directory.Value.Where(c => ufs.Contains(c.Uf) && c.Id != g.ClubId).OrderByDescending(c => c.Strength).ThenBy(c => c.Id, StringComparer.Ordinal).Take(7)
            .Select(c => new Club(c.Id, c.Name, c.Short, c.Color, c.Strength)).ToList();
        if (others.Count < 7) return;
        AddKnown(g, others);
        var cup = ClubCompetitions.Add(g, id, name, [g.ClubId, .. others.Select(c => c.Id)], [.. rounds], champion, 1);
        cup.Light = true; cup.EntryPrize = entry; cup.StagePrizes = [.. stages]; cup.RunnerUpPrize = runnerUp; cup.Format = format;
    }

    // Supercopa do Brasil: the league champion against the Copa do Brasil champion of the previous season (champion R$ 11,64 mi,
    // runner-up R$ 6,35 mi). Created only when the managed club of a Série A career is one of the two.
    public static void CreateSupercopa(GameState g)
    {
        if (SeriesMovement.SeriesOf(g) != "A" || g.PastSeasons.Count == 0 || g.Life == null) return;
        var past = g.PastSeasons[^1];
        var copa = g.Life.CupHistory.LastOrDefault(a => a.Competition.Id == "copa" && a.Season == past.Season)?.Competition.Champion ?? "";
        var league = past.Champion;
        if (copa == "" || league == "" || (g.ClubId != copa && g.ClubId != league)) return;
        var rival = g.ClubId == league ? copa : league;
        if (rival == g.ClubId) rival = past.Table.Select(t => t.ClubId).FirstOrDefault(id => id != g.ClubId) ?? "";
        if (g.Clubs.All(c => c.Id != rival)) return;
        var cup = ClubCompetitions.Add(g, "supercopa", "Supercopa do Brasil", [g.ClubId, rival], [1], 11_640_000, 1);
        cup.RunnerUpPrize = 6_350_000;
        cup.Format = "Supercopa do Brasil: campeão do Brasileirão contra o campeão da Copa do Brasil (ou o vice da liga, se for o mesmo clube), em jogo único. Campeão R$ 11,64 mi e vice R$ 6,35 mi.";
    }

    // The mini league of one group of the Série C second phase.
    public static List<LeagueRules.Standing> GroupTable(GameState g, ClubCompetition competition, string tie)
    {
        var matches = competition.Matches.Where(m => m.Tie == tie).ToList();
        var ids = matches.SelectMany(m => new[] { m.Fixture.Home, m.Fixture.Away }).Distinct().ToList();
        return LeagueRules.Table(SerieD.AllClubs(g).Where(c => ids.Contains(c.Id)).ToList(), matches.Select(m => m.Fixture).ToList(), g.LotterySeed + ":" + g.Season + ":" + tie).ToList();
    }

    // Called by ClubCompetitions.Complete for the matches of a group stage.
    public static void OnGroupMatch(GameState g, ClubCompetition competition)
    {
        if (competition.Advanced.Count > 0 || competition.Matches.Any(m => m.Fixture.HomeGoals == null)) return;
        if (competition.Id == "state") { StartStateKnockout(g, competition); return; }
        if (competition.Id is "libertadores-groups" or "sul-americana-groups") { Continental.EndGroups(g, competition); return; }
        if (competition.Id != "serie-c-groups") return;
        var tables = new[] { GroupTable(g, competition, "g1"), GroupTable(g, competition, "g2") };
        competition.Advanced = tables.SelectMany(t => t.Take(2).Select(r => r.Club.Id)).ToList();
        competition.Champion = ""; // the competition champion is the winner of the final
        string Name(string id) => g.Clubs.FirstOrDefault(c => c.Id == id)?.Name ?? id;
        g.News.Insert(0, "Série C: sobem para a Série B " + string.Join(", ", competition.Advanced.Select(Name)) + ".");
        // Prize table of the series by final standing: runners-up of the groups share the 3rd/4th prizes, the others the 5th to 8th.
        for (int k = 0; k < tables.Length; k++)
        {
            int at = tables[k].FindIndex(r => r.Club.Id == g.ClubId);
            if (at >= 0) Pay(g, "Série C · segunda fase: premiação", at < 2 ? 900_000 : 350_000);
        }
        var final = ClubCompetitions.Add(g, "serie-c-final", "Série C · final", [tables[0][0].Club.Id, tables[1][0].Club.Id], [SerieCFinalRound], 1_100_000, 2);
        final.RunnerUpPrize = 600_000;
        final.Format = "Final da Série C: os líderes dos dois grupos, em ida e volta. Vence o melhor agregado e, no empate, os pênaltis. A final não decide o acesso, que já está garantido aos quatro semifinalistas.";
    }

    // Did the club earn promotion this season, besides finishing in the direct places of the table?
    public static bool EarnedPromotion(GameState g, string series, int position)
    {
        var competitions = g.Life?.Competitions ?? [];
        return series switch
        {
            "B" => position <= 2 || competitions.Any(c => c.Id.StartsWith("serie-b-playoff-", StringComparison.Ordinal) && c.Champion == g.ClubId),
            "C" => competitions.Any(c => c.Id == "serie-c-groups" && c.Advanced.Contains(g.ClubId)),
            _ => false
        };
    }
}

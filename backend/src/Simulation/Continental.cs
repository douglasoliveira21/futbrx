namespace Brasileirao;

// Libertadores, Pré-Libertadores and Sul-Americana for the managed club of a Série A career, from the places resolved by
// Qualification and the national championships of the other nine countries (SouthAmerica). Only the matches of the managed club
// have squads, the rest of each competition is resolved from club strength. Prizes follow the 2026 tables supplied for the game.
public static class Continental
{
    const long Mi = 1_000_000;
    static readonly string[] Prefix = ["Club Atlético", "Deportivo", "Sporting", "Unión", "Real", "Independiente"];
    static readonly string[] Names = ["Andino", "Charrúa", "Pampeano", "Altiplano", "Caribe", "Llanero", "Austral", "Cóndor"];

    // ---- Real clubs of the other nine countries (SouthAmerica.cs) ----
    static string CountryOf(GameState g, string clubId) => g.Clubs.Any(c => c.Id == clubId) ? "BRA" : SouthAmerica.CountryOf(clubId)?.Code ?? "?";
    static List<Club> Draw(GameState g, IEnumerable<Club> clubs, string key) => clubs.OrderBy(c => ClubLife.Seed(g, $"{key}:{g.Season}:{c.Id}")).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
    static Club ClubOf(GameState g, string id) => g.Clubs.FirstOrDefault(c => c.Id == id) ?? SouthAmerica.Find(id) ?? g.KnownClubs.First(c => c.Id == id);

    // One match (or a pair): the better club tends to win; a draw goes to a seeded shootout.
    static string Duel(GameState g, string key, Club a, Club b)
    {
        var (h, w) = SerieD.Score(a.Strength, b.Strength, ClubLife.Seed(g, key + a.Id + b.Id));
        if (h != w) return h > w ? a.Id : b.Id;
        return new Random(ClubLife.Seed(g, key + "pen" + a.Id + b.Id)).NextDouble() < .5 + (a.Strength - b.Strength) / 400.0 ? a.Id : b.Id;
    }

    // Libertadores entries: Brazil's places come from the career (Qualification); the others from their national championships.
    static (List<Club> Direct, List<Club> Pre) LibertadoresEntries(GameState g)
    {
        var direct = new List<Club>(); var pre = new List<Club>();
        foreach (var berth in g.Qualification?.Berths ?? [])
        {
            var club = g.Clubs.FirstOrDefault(c => c.Id == berth.ClubId); if (club == null) continue;
            if (berth.Competition == Qualification.Libertadores) direct.Add(club); else if (berth.Competition == Qualification.Pre) pre.Add(club);
        }
        foreach (var result in SouthAmerica.Ensure(g).Results)
            for (int i = 0; i < result.Libertadores.Count; i++)
            {
                var club = SouthAmerica.Find(result.Libertadores[i])!;
                (i < (result.Code == "ARG" ? 3 : 2) ? direct : pre).Add(club);
            }
        return (direct, pre);
    }

    // Pré-Libertadores of the other clubs: single-match rounds in which the weakest clubs meet until the places left in the groups are filled.
    static List<Club> PreWinners(GameState g, List<Club> pool, int places)
    {
        var alive = pool.OrderByDescending(c => c.Strength).ThenBy(c => c.Id, StringComparer.Ordinal).ToList(); int round = 0;
        while (alive.Count > places && alive.Count > 1)
        {
            int ties = Math.Min(alive.Count - places, alive.Count / 2);
            var playing = alive.Skip(alive.Count - ties * 2).ToList(); var winners = new List<Club>();
            for (int i = 0; i < ties; i++) { var a = playing[i]; var b = playing[playing.Count - 1 - i]; winners.Add(Duel(g, $"pre:{g.Season}:{round}:{i}", a, b) == a.Id ? a : b); }
            alive = alive.Take(alive.Count - ties * 2).Concat(winners).OrderByDescending(c => c.Strength).ThenBy(c => c.Id, StringComparer.Ordinal).ToList(); round++;
        }
        return alive;
    }

    // The 32 clubs of the Libertadores group stage. A club that reached it through the Pré (the manager's) takes the last place.
    static List<Club> LibertadoresField(GameState g)
    {
        var (direct, pre) = LibertadoresEntries(g);
        var mine = g.Clubs.First(c => c.Id == g.ClubId);
        bool forced = !direct.Any(c => c.Id == g.ClubId);
        // Brazilian clubs of the Pré that are not the manager's are resolved like the others.
        var pool = pre.Where(c => c.Id != g.ClubId).ToList();
        var field = direct.ToList(); if (forced) field.Add(mine);
        field.AddRange(PreWinners(g, pool, Math.Max(0, 32 - field.Count)));
        return field.DistinctBy(c => c.Id).Take(32).ToList();
    }

    // The 32 clubs of the Sudamericana group stage: Brazil and Argentina enter directly, each other country sends two (winners of
    // a tie between its first and fourth and second and third), and the best of the rest complete the field.
    static List<Club> SulaField(GameState g)
    {
        var field = new List<Club>(); var rest = new List<Club>();
        foreach (var berth in g.Qualification?.Berths ?? []) if (berth.Competition == Qualification.Sula && g.Clubs.FirstOrDefault(c => c.Id == berth.ClubId) is { } c) field.Add(c);
        var mine = g.Clubs.First(c => c.Id == g.ClubId); if (field.All(c => c.Id != mine.Id)) field.Add(mine);
        foreach (var result in SouthAmerica.Ensure(g).Results)
        {
            var list = result.Sudamericana.Select(id => SouthAmerica.Find(id)!).ToList();
            if (result.Code == "ARG") { field.AddRange(list); continue; }
            if (list.Count < 4) { field.AddRange(list); continue; }
            var w1 = Duel(g, $"sula1:{g.Season}:{result.Code}:a", list[0], list[3]); var w2 = Duel(g, $"sula1:{g.Season}:{result.Code}:b", list[1], list[2]);
            foreach (var club in list) (club.Id == w1 || club.Id == w2 ? field : rest).Add(club);
        }
        field.AddRange(rest.OrderByDescending(c => c.Strength).Take(Math.Max(0, 32 - field.Count)));
        return field.DistinctBy(c => c.Id).Take(32).ToList();
    }

    // Draw of 8 groups of four: pots by strength, one club of each pot per group, never two clubs of the same country when it can be avoided.
    // The group of the manager's club comes first.
    static List<List<Club>> DrawGroups(GameState g, string kind, List<Club> field)
    {
        var pots = field.OrderByDescending(c => c.Strength).ThenBy(c => c.Id, StringComparer.Ordinal).Chunk(8).Select(p => p.ToList()).ToList();
        var groups = Enumerable.Range(0, 8).Select(_ => new List<Club>()).ToList();
        foreach (var (pot, potIndex) in pots.Select((p, i) => (p, i)))
        {
            var order = Enumerable.Range(0, 8).OrderBy(i => ClubLife.Seed(g, $"draw:{kind}:{g.Season}:{potIndex}:{i}")).ToList();
            foreach (var club in pot.OrderBy(c => ClubLife.Seed(g, $"drawclub:{kind}:{g.Season}:{c.Id}")))
            {
                var target = order.FirstOrDefault(i => groups[i].Count == potIndex && groups[i].All(x => CountryOf(g, x.Id) != CountryOf(g, club.Id)), -1);
                if (target < 0) target = order.First(i => groups[i].Count == potIndex);
                groups[target].Add(club); order.Remove(target);
            }
        }
        int mine = groups.FindIndex(grp => grp.Any(c => c.Id == g.ClubId));
        if (mine > 0) (groups[0], groups[mine]) = (groups[mine], groups[0]);
        return groups;
    }

    // Called when the season's competitions are created.
    public static void CreateSeason(GameState g)
    {
        if (g.Life == null || SeriesMovement.SeriesOf(g) != "A" || g.DataVersion.StartsWith("fictional-", StringComparison.Ordinal)) return;
        if (g.Qualification == null) { if (g.PastSeasons.Count > 0) return; g.Qualification = Qualification.Seed(g); }
        var mine = g.Qualification.Berths.FirstOrDefault(b => b.ClubId == g.ClubId);
        if (g.Qualification.Season == g.Season && g.PastSeasons.Count == 0) g.News.Insert(0, mine == null ? "O clube não tem vaga em competição internacional nesta temporada." : $"Competição internacional da temporada: {mine.Origin}.");
        if (mine == null) return;
        if (mine.Competition == Qualification.Pre) StartPre(g);
        else StartGroups(g, mine.Competition == Qualification.Libertadores ? Qualification.Libertadores : Qualification.Sula);
    }

    static void StartPre(GameState g)
    {
        var (_, preClubs) = LibertadoresEntries(g);
        var opponents = Draw(g, preClubs.Where(c => c.Id != g.ClubId && SouthAmerica.CountryOf(c.Id) != null), "pre").Take(7).ToList();
        SeriesFormats.AddKnown(g, opponents);
        var field = new List<string> { g.ClubId }; field.AddRange(opponents.Select(c => c.Id));
        var pre = ClubCompetitions.Add(g, "pre-libertadores", "Pré-Libertadores", field, [2, 4, 6], 0, 1);
        pre.Light = true; pre.Promotion = true; pre.StagePrizes = [2 * Mi, 2_500_000, 3 * Mi];
        pre.Format = "Pré-Libertadores em três fases de jogo único (cotas de R$ 2 mi, R$ 2,5 mi e R$ 3 mi), contra clubes reais dos outros países. Quem vence a terceira fase vai à fase de grupos da Libertadores; o eliminado nela é transferido para a fase de grupos da Sul-Americana.";
    }

    static void StartGroups(GameState g, string kind)
    {
        bool lib = kind == Qualification.Libertadores;
        var draw = DrawGroups(g, kind, lib ? LibertadoresField(g) : SulaField(g));
        var opponents = draw[0].Where(c => c.Id != g.ClubId).ToList();
        SeriesFormats.AddKnown(g, draw.SelectMany(grp => grp).Where(c => g.Clubs.All(x => x.Id != c.Id)));
        var groups = new ClubCompetition
        {
            Id = kind + "-groups", Name = lib ? "Libertadores · fase de grupos" : "Sul-Americana · fase de grupos", Rounds = Enumerable.Range(lib ? 8 : 9, 6).ToList(), Legs = 1, GroupStage = true, Light = true,
            EntryPrize = lib ? 15 * Mi : 4_500_000, WinPrize = lib ? 1_700_000 : 625_000,
            Format = lib ? "Grupo de quatro clubes em ida e volta, sorteado com clubes reais de outros países (nunca dois do mesmo país). Participação R$ 15 mi e R$ 1,7 mi por vitória. Os dois primeiros vão às oitavas; o terceiro é transferido para a Sul-Americana."
                         : "Grupo de quatro clubes em ida e volta, sorteado com clubes reais de outros países. Participação R$ 4,5 mi e R$ 625 mil por vitória. O primeiro vai direto às oitavas; o segundo joga o playoff contra um terceiro da Libertadores."
        };
        int index = 0;
        SeriesFormats.AddRoundRobin(g, groups, "g1", [g.ClubId, .. opponents.Select(c => c.Id)], lib ? 8 : 9, true, ref index);
        g.Life!.Competitions.Add(groups);
    }

    // Table of a simulated group: clubs by points, goal difference, goals and strength.
    static List<Club> Simulate(GameState g, string key, List<Club> clubs)
    {
        var points = clubs.ToDictionary(c => c.Id, _ => new int[3]); // points, goal difference, goals
        foreach (var home in clubs) foreach (var away in clubs)
        {
            if (home.Id == away.Id) continue;
            var (h, a) = SerieD.Score(home.Strength, away.Strength, ClubLife.Seed(g, $"cgroup:{key}:{g.Season}:{home.Id}:{away.Id}"));
            void Apply(string id, int scored, int against) { var p = points[id]; p[1] += scored - against; p[2] += scored; p[0] += scored > against ? 3 : scored == against ? 1 : 0; }
            Apply(home.Id, h, a); Apply(away.Id, a, h);
        }
        return clubs.OrderByDescending(c => points[c.Id][0]).ThenByDescending(c => points[c.Id][1]).ThenByDescending(c => points[c.Id][2]).ThenByDescending(c => c.Strength).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
    }

    // The managed club's group is over: the other seven groups are resolved from strength and the knockouts are created.
    public static void EndGroups(GameState g, ClubCompetition groups)
    {
        bool lib = groups.Id.StartsWith(Qualification.Libertadores, StringComparison.Ordinal);
        var draw = DrawGroups(g, lib ? Qualification.Libertadores : Qualification.Sula, lib ? LibertadoresField(g) : SulaField(g));
        var own = SeriesFormats.GroupTable(g, groups, "g1").Select(r => r.Club).ToList();
        var tables = new List<List<Club>> { own };
        for (int k = 1; k < 8; k++) tables.Add(Simulate(g, groups.Id + k, draw[k]));
        var winners = tables.Select(t => t[0]).ToList(); var runners = tables.Select(t => t[1]).ToList();
        int rank = own.FindIndex(c => c.Id == g.ClubId) + 1;
        g.News.Insert(0, $"{groups.Name}: o clube terminou o grupo em {rank}º lugar.");
        if (lib)
        {
            // Round of 16: each group winner meets the runner-up of the neighbouring group.
            var ties = Enumerable.Range(0, 8).SelectMany(k => new[] { winners[k].Id, runners[k ^ 1].Id }).ToList();
            var cup = ClubCompetitions.Add(g, Qualification.Libertadores, "Copa Libertadores", ties, [16, 20, 24, 28], 125 * Mi, 2);
            cup.Light = true; cup.FinalSingle = true; cup.StagePrizes = [6_250_000, 8_500_000, 11_500_000, 0]; cup.RunnerUpPrize = 35 * Mi;
            cup.Format = "Mata-mata a partir das oitavas, ida e volta (final em jogo único). Cotas de 2026: oitavas R$ 6,25 mi, quartas R$ 8,5 mi, semifinal R$ 11,5 mi, vice R$ 35 mi e campeão R$ 125 mi. Os outros clubes saem da força, sem transmissão.";
            if (rank == 3) StartSulaFromThird(g, runners, winners, tables.Select(t => t[2]).ToList());
        }
        else
        {
            // Playoffs: group runners-up against the thirds of the Libertadores groups; the group winners enter at the round of 16.
            var libTables = DrawGroups(g, Qualification.Libertadores, LibertadoresField(g)).Select((grp, k) => Simulate(g, "libthirds" + k, grp)).ToList();
            var thirds = libTables.Select(t => t[2]).ToList();
            SeriesFormats.AddKnown(g, thirds.Where(c => g.Clubs.All(x => x.Id != c.Id)));
            BuildSula(g, runners.Select(c => c.Id).ToList(), thirds.Select(c => c.Id).ToList(), winners.Select(c => c.Id).ToList());
        }
    }

    // A third place of the Libertadores group goes to the Sul-Americana playoffs, against runners-up and winners of the Sul-Americana groups.
    static void StartSulaFromThird(GameState g, List<Club> libRunners, List<Club> libWinners, List<Club> libThirds)
    {
        var sula = DrawGroups(g, Qualification.Sula, SulaField(g)).Select((grp, k) => Simulate(g, "sulagroups" + k, grp)).ToList();
        var runners = sula.Select(t => t[1].Id).ToList(); var winners = sula.Select(t => t[0].Id).ToList();
        SeriesFormats.AddKnown(g, sula.SelectMany(t => t.Take(2)).Concat(libThirds).Where(c => g.Clubs.All(x => x.Id != c.Id)));
        BuildSula(g, runners, libThirds.Select(c => c.Id).ToList(), winners);
        g.News.Insert(0, "Terceiro colocado do grupo da Libertadores: o clube é transferido para os playoffs da Sul-Americana.");
    }

    static void BuildSula(GameState g, List<string> runners, List<string> thirds, List<string> winners)
    {
        var participants = Enumerable.Range(0, 8).SelectMany(k => new[] { runners[k], thirds[k] }).ToList();
        var cup = ClubCompetitions.Add(g, Qualification.Sula, "Copa Sul-Americana", participants, [18, 22, 26, 30, 34], 50 * Mi, 2);
        cup.Light = true; cup.FinalSingle = true; cup.Inject = winners; cup.StagePrizes = [2_500_000, 3 * Mi, 3_500_000, 4 * Mi, 0]; cup.RunnerUpPrize = 12_500_000;
        cup.Format = "Playoffs (segundos do grupo × terceiros da Libertadores), oitavas com os líderes dos grupos, quartas e semifinal em ida e volta e final em jogo único. Cotas de 2026: playoffs R$ 2,5 mi, oitavas R$ 3 mi, quartas R$ 3,5 mi, semifinal R$ 4 mi, vice R$ 12,5 mi e campeão R$ 50 mi.";
    }

    // The loser of the third phase of the Pré-Libertadores goes to the Sul-Americana groups; the winner to the Libertadores groups.
    public static void OnChampion(GameState g, ClubCompetition competition)
    {
        if (competition.Id != "pre-libertadores" || g.Life == null) return;
        var final = competition.Matches.Where(m => m.Stage == competition.Stage).ToList();
        if (final.Count != 1) return;
        var f = final[0].Fixture; var mine = f.Home == g.ClubId || f.Away == g.ClubId;
        if (!mine) return;
        if (competition.Champion == g.ClubId) { g.News.Insert(0, "Pré-Libertadores: vaga na fase de grupos da Libertadores conquistada."); StartGroups(g, Qualification.Libertadores); }
        else { g.News.Insert(0, "Eliminado na terceira fase da Pré-Libertadores: o clube é transferido para a fase de grupos da Sul-Americana."); StartGroups(g, Qualification.Sula); }
    }
}

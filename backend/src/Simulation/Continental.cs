namespace Brasileirao;

// Libertadores, Pré-Libertadores and Sul-Americana for the managed club of a Série A career, from the places resolved by
// Qualification. Foreign clubs are illustrative (fictional); only the matches of the managed club have squads, the rest of each
// competition is resolved from club strength. Prizes follow the 2026 tables supplied for the game.
public static class Continental
{
    const long Mi = 1_000_000;
    static readonly string[] Prefix = ["Club Atlético", "Deportivo", "Sporting", "Unión", "Real", "Independiente"];
    static readonly string[] Names = ["Andino", "Charrúa", "Pampeano", "Altiplano", "Caribe", "Llanero", "Austral", "Cóndor"];

    // 48 illustrative clubs from the other South American countries.
    public static List<Club> Foreign(GameState g)
    {
        var list = new List<Club>();
        for (int p = 0; p < Prefix.Length; p++)
            for (int n = 0; n < Names.Length; n++)
            {
                int i = p * Names.Length + n;
                list.Add(new Club($"int-{i + 1:00}", $"{Prefix[p]} {Names[n]}", Prefix[p][..1].ToUpperInvariant() + Names[n][..2].ToUpperInvariant(), "#7a8aa6", 58 + i * 7 % 23));
            }
        return list;
    }
    static List<Club> Draw(GameState g, IEnumerable<Club> clubs, string key) => clubs.OrderBy(c => ClubLife.Seed(g, $"{key}:{g.Season}:{c.Id}")).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();

    // Called when the season's competitions are created.
    public static void CreateSeason(GameState g)
    {
        if (g.Life == null || SeriesMovement.SeriesOf(g) != "A" || g.DataVersion.StartsWith("fictional-", StringComparison.Ordinal)) return;
        if (g.Qualification == null) { if (g.PastSeasons.Count > 0) return; g.Qualification = Qualification.Seed(g); }
        var mine = g.Qualification.Berths.FirstOrDefault(b => b.ClubId == g.ClubId);
        if (g.Qualification.Season == g.Season && g.PastSeasons.Count == 0) g.News.Insert(0, mine == null ? "O clube não tem vaga em competição internacional nesta temporada." : $"Competição internacional da temporada: {mine.Origin}.");
        if (mine == null) return;
        SeriesFormats.AddKnown(g, Foreign(g));
        if (mine.Competition == Qualification.Pre) StartPre(g);
        else StartGroups(g, mine.Competition == Qualification.Libertadores ? Qualification.Libertadores : Qualification.Sula);
    }

    static void StartPre(GameState g)
    {
        var field = new List<string> { g.ClubId }; field.AddRange(Draw(g, Foreign(g), "pre").Take(7).Select(c => c.Id));
        var pre = ClubCompetitions.Add(g, "pre-libertadores", "Pré-Libertadores", field, [2, 4, 6], 0, 1);
        pre.Light = true; pre.Promotion = true; pre.StagePrizes = [2 * Mi, 2_500_000, 3 * Mi];
        pre.Format = "Pré-Libertadores em três fases de jogo único (cotas de R$ 2 mi, R$ 2,5 mi e R$ 3 mi). Quem vence a terceira fase vai à fase de grupos da Libertadores; o eliminado nela é transferido para a fase de grupos da Sul-Americana.";
    }

    static void StartGroups(GameState g, string kind)
    {
        bool lib = kind == Qualification.Libertadores;
        var opponents = Draw(g, Foreign(g), "group-" + kind).Take(3).ToList();
        SeriesFormats.AddKnown(g, opponents);
        var groups = new ClubCompetition
        {
            Id = kind + "-groups", Name = lib ? "Libertadores · fase de grupos" : "Sul-Americana · fase de grupos", Rounds = Enumerable.Range(lib ? 8 : 9, 6).ToList(), Legs = 1, GroupStage = true, Light = true,
            EntryPrize = lib ? 15 * Mi : 4_500_000, WinPrize = lib ? 1_700_000 : 625_000,
            Format = lib ? "Grupo de quatro clubes em ida e volta (o seu grupo, com adversários ilustrativos de outros países). Participação R$ 15 mi e R$ 1,7 mi por vitória. Os dois primeiros vão às oitavas; o terceiro é transferido para a Sul-Americana."
                         : "Grupo de quatro clubes em ida e volta. Participação R$ 4,5 mi e R$ 625 mil por vitória. O primeiro vai direto às oitavas; o segundo joga o playoff contra um terceiro da Libertadores."
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
        var own = SeriesFormats.GroupTable(g, groups, "g1").Select(r => r.Club).ToList();
        var ownIds = own.Select(c => c.Id).ToHashSet();
        var brazilian = (g.Qualification?.Berths ?? []).Where(b => b.ClubId != g.ClubId && b.Competition == (lib ? Qualification.Libertadores : Qualification.Sula))
            .Select(b => g.Clubs.FirstOrDefault(c => c.Id == b.ClubId)).Where(c => c != null).Select(c => c!).Take(7).ToList();
        var pool = Draw(g, Foreign(g).Where(c => !ownIds.Contains(c.Id)), "pool-" + groups.Id).ToList();
        var tables = new List<List<Club>> { own };
        for (int k = 0; k < 7; k++)
        {
            var members = new List<Club>(); if (k < brazilian.Count) members.Add(brazilian[k]);
            while (members.Count < 4) { var next = pool.First(c => members.All(m => m.Id != c.Id) && !tables.Skip(1).SelectMany(t => t).Any(t => t.Id == c.Id)); members.Add(next); }
            tables.Add(Simulate(g, groups.Id + k, members));
        }
        var winners = tables.Select(t => t[0]).ToList(); var runners = tables.Select(t => t[1]).ToList();
        int rank = own.FindIndex(c => c.Id == g.ClubId) + 1;
        string Name(Club c) => c.Name;
        g.News.Insert(0, $"{groups.Name}: o clube terminou o grupo em {rank}º lugar.");
        if (lib)
        {
            // Round of 16: each group winner meets the runner-up of the neighbouring group.
            var ties = Enumerable.Range(0, 8).SelectMany(k => new[] { winners[k].Id, runners[k ^ 1].Id }).ToList();
            var cup = ClubCompetitions.Add(g, Qualification.Libertadores, "Copa Libertadores", ties, [16, 20, 24, 28], 125 * Mi, 2);
            cup.Light = true; cup.FinalSingle = true; cup.StagePrizes = [6_250_000, 8_500_000, 11_500_000, 0]; cup.RunnerUpPrize = 35 * Mi;
            cup.Format = "Mata-mata a partir das oitavas, ida e volta (final em jogo único). Cotas de 2026: oitavas R$ 6,25 mi, quartas R$ 8,5 mi, semifinal R$ 11,5 mi, vice R$ 35 mi e campeão R$ 125 mi. Os outros clubes saem da força, sem transmissão.";
            if (rank == 3) StartSulaFromThird(g, runners, winners);
        }
        else
        {
            // Playoffs: group runners-up against illustrative thirds of the Libertadores; the group winners enter at the round of 16.
            var thirds = pool.Where(c => tables.SelectMany(t => t).All(t => t.Id != c.Id)).Take(8).ToList();
            BuildSula(g, runners.Select(c => c.Id).ToList(), thirds.Select(c => c.Id).ToList(), winners.Select(c => c.Id).ToList());
        }
    }

    // A third place of the Libertadores group goes to the Sul-Americana playoffs.
    static void StartSulaFromThird(GameState g, List<Club> runners, List<Club> libWinners)
    {
        var pool = Draw(g, Foreign(g).Where(c => runners.All(r => r.Id != c.Id) && libWinners.All(w => w.Id != c.Id)), "sula-from-lib").ToList();
        SeriesFormats.AddKnown(g, pool.Take(23));
        var rivals = pool.Take(8).Select(c => c.Id).ToList(); var thirds = new List<string> { g.ClubId }; thirds.AddRange(pool.Skip(8).Take(7).Select(c => c.Id));
        var groupWinners = pool.Skip(15).Take(8).Select(c => c.Id).ToList();
        BuildSula(g, rivals, thirds, groupWinners);
        g.News.Insert(0, "Terceiro colocado do grupo da Libertadores: o clube é transferido para os playoffs da Sul-Americana.");
    }

    static void BuildSula(GameState g, List<string> runners, List<string> thirds, List<string> winners)
    {
        var participants = Enumerable.Range(0, 8).SelectMany(k => new[] { runners[k], thirds[k] }).ToList();
        SeriesFormats.AddKnown(g, Foreign(g));
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

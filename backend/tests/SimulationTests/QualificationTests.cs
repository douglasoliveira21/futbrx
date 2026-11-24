using Brasileirao;

// International places: the distribution engine and the continental competitions with their prizes.
public static class QualificationTests
{
    const long Mi = 1_000_000;
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }
    static readonly List<string> Table = Enumerable.Range(1, 20).Select(i => $"c{i:00}").ToList();
    static bool Eligible(string c) => string.CompareOrdinal(c, "c17") < 0;
    static List<Berth> Run(string champ, string runner, params string[] continental) => Qualification.Resolve(Table, Eligible, champ, runner, continental, Qualification.RulesFor(2027));
    static string[] Of(List<Berth> berths, string competition) => berths.Where(b => b.Competition == competition).Select(b => b.ClubId).Order().ToArray();
    static string[] Ids(params int[] n) => n.Select(i => $"c{i:00}").Order().ToArray();

    // Plays every pending match of the competitions of a career by decree: the club wins (or loses) its own matches.
    static void Decree(GameState g, bool win, params string[] only)
    {
        for (int pass = 0; pass < 40; pass++)
        {
            var pending = g.Life!.Competitions.Where(c => only.Length == 0 || only.Contains(c.Id)).SelectMany(c => c.Matches).Where(m => m.Fixture.HomeGoals == null).OrderBy(m => m.Fixture.Round).ThenBy(m => m.Leg).ToList();
            if (pending.Count == 0) break;
            foreach (var m in pending)
            {
                var f = m.Fixture; bool mine = f.Home == g.ClubId || f.Away == g.ClubId, home = f.Home == g.ClubId;
                var competition = g.Life.Competitions.First(c => c.Matches.Contains(m));
                bool single = competition.Legs == 1 || m.Single;
                bool homeWins = mine ? home == win : m.Leg == 1;
                f.HomeGoals = homeWins ? 1 : 0; f.AwayGoals = homeWins ? 0 : (mine || single ? 1 : 0);
                if (!mine && !single && m.Leg == 2) { f.HomeGoals = 0; f.AwayGoals = 0; }
                if (competition.GroupStage && !mine) { f.HomeGoals = 0; f.AwayGoals = 0; }
                ClubCompetitions.Complete(g, m);
            }
        }
    }
    static GameState Career(string id, Berth? berth)
    {
        var g = Game.New(id, Game.Clubs2026Catalog()); g.Life!.Competitions.Clear();
        g.Qualification = new QualificationState { Season = g.Season, Berths = berth == null ? [] : [berth] };
        return g;
    }
    static long Money(GameState g, string prefix) => g.Ledger.Where(l => l.Description.StartsWith(prefix)).Sum(l => l.Amount);

    public static void Run()
    {
        // --- Engine: the regulation of 2027, in every combination ---
        var plain = Run("c15", "c09");
        Check(Of(plain, "libertadores").SequenceEqual(Ids(1, 2, 3, 4, 15)) && Of(plain, "pre-libertadores").SequenceEqual(Ids(5, 9)) && Of(plain, "sul-americana").SequenceEqual(Ids(6, 7, 8, 10, 11, 12)), "Campeão e vice da Copa fora do G4: campeão nos grupos, vice na Pré, Sul-Americana pelos seis seguintes");
        var inZone = Run("c03", "c09");
        Check(Of(inZone, "libertadores").SequenceEqual(Ids(1, 2, 3, 4, 9)) && Of(inZone, "pre-libertadores").SequenceEqual(Ids(5, 6)) && Of(inZone, "sul-americana").SequenceEqual(Ids(7, 8, 10, 11, 12, 13)), "Campeão da Copa já no G4: a vaga direta da Copa passa ao vice e a vaga do vice na Pré vai à tabela");
        var champs = Run("c12", "c14", "c02", "c18");
        Check(Of(champs, "libertadores").SequenceEqual(Ids(1, 2, 3, 4, 5, 12)) && champs.Single(b => b.ClubId == "c05").Origin.Contains("remanejada"), "Campeão continental que já estava no G4 libera a vaga: a zona se expande ao 5º");
        var extra = Run("c13", "c14", "c10");
        Check(Of(extra, "libertadores").SequenceEqual(Ids(1, 2, 3, 4, 10, 13)) && extra.Single(b => b.ClubId == "c10").Origin.Contains("adicional"), "Campeão continental fora do G4 ocupa vaga adicional");
        var relegatedChampion = Run("c18", "c19");
        Check(Of(relegatedChampion, "libertadores").SequenceEqual(Ids(1, 2, 3, 4)) && !relegatedChampion.Any(b => b.ClubId is "c18" or "c19") && Of(relegatedChampion, "pre-libertadores").Length == 2, "Clube rebaixado não participa e as vagas descem na tabela");
        var none = Run("", "");
        Check(Of(none, "libertadores").SequenceEqual(Ids(1, 2, 3, 4)) && Of(none, "pre-libertadores").SequenceEqual(Ids(5, 6)) && Of(none, "sul-americana").SequenceEqual(Ids(7, 8, 9, 10, 11, 12)), "Sem resultados de copa a tabela padrão vale: 1º a 4º, Pré do 5º e 6º, Sul-Americana do 7º ao 12º");
        // Property: whatever the combination, nobody has two places, nobody ineligible is in, and the sizes are bounded.
        var rng = new Random(7); int combos = 0;
        for (int i = 0; i < 400; i++)
        {
            string P() => rng.Next(4) == 0 ? "" : Table[rng.Next(20)];
            var result = Run(P(), P(), P(), P());
            Check2(result.Select(b => b.ClubId).Distinct().Count() == result.Count && result.All(b => Eligible(b.ClubId)) && Of(result, "libertadores").Length is >= 4 and <= 9 && Of(result, "sul-americana").Length == 6, "combinação " + i);
            combos++;
        }
        Check(combos == 400, "400 combinações aleatórias de campeões e vices sem vagas duplicadas, nem inelegíveis, com Sul-Americana sempre de seis clubes");
        // Regulation by season.
        Qualification.Rules.Add(new(2030, 5, 0, 4, false));
        var later = Qualification.Resolve(Table, Eligible, "", "", ["c02"], Qualification.RulesFor(2031));
        bool ok = Of(later, "libertadores").Length == 5 && Of(later, "pre-libertadores").Length == 0 && Of(later, "sul-americana").Length == 4 && Qualification.RulesFor(2029).LeagueDirect == 4;
        Qualification.Rules.RemoveAt(Qualification.Rules.Count - 1);
        Check(ok, "Regulamento pode mudar por temporada sem tocar no motor");

        // --- Season flow: first-season places, close of the season, projection ---
        var a = Game.New("pal", Game.Clubs2026Catalog());
        Check(a.Qualification != null && a.Qualification.Berths.Select(b => b.ClubId).Distinct().Count() == a.Qualification.Berths.Count && a.Qualification.Berths.Count >= 11, "Primeira temporada recebe vagas internacionais pela força dos clubes, sem repetição");
        var proj = System.Text.Json.JsonSerializer.SerializeToElement(Qualification.Projection(a), Game.Json);
        Check(proj.GetProperty("zones").EnumerateObject().Count() == 20 && proj.GetProperty("zones").EnumerateObject().Count(z => z.Value.GetProperty("zone").GetString() == "relegation") == 4, "Projeção da tabela traz as zonas dos 20 clubes e os quatro rebaixados");

        // --- Libertadores: groups, knockout and every prize ---
        var lib = Career("pal", new("pal", "libertadores", "Brasileirão (1º)"));
        Continental.CreateSeason(lib);
        var groups = lib.Life!.Competitions.Single(c => c.Id == "libertadores-groups");
        Check(groups.GroupStage && groups.Matches.Count == 12 && groups.EntryPrize == 15 * Mi && groups.WinPrize == 1_700_000 && groups.Matches.Any(m => m.Fixture.Home == "pal"), "Libertadores: grupo de quatro com seis rodadas, participação R$ 15 mi e R$ 1,7 mi por vitória");
        Decree(lib, true);
        var cup = lib.Life.Competitions.Single(c => c.Id == "libertadores");
        Check(cup.Champion == "pal" && cup.Matches.Count(m => m.Stage == 0) == 16 && cup.Matches.Count(m => m.Stage == 3) == 1 && cup.Matches.Single(m => m.Stage == 3).Single, "Libertadores: oitavas de ida e volta com 16 clubes e final em jogo único");
        Check(Money(lib, "Libertadores") + Money(lib, "Copa Libertadores") + Money(lib, "Título: Copa Libertadores") == 15 * Mi + 6 * 1_700_000 + 6_250_000 + 8_500_000 + 11_500_000 + 125 * Mi, "Campeão da Libertadores recebe R$ 176,45 mi (participação, seis vitórias, oitavas, quartas, semifinal e título)");
        var libLose = Career("pal", new("pal", "libertadores", "Brasileirão (1º)")); Continental.CreateSeason(libLose);
        Decree(libLose, true, "libertadores-groups");
        var finalMatch = libLose.Life!.Competitions.Single(c => c.Id == "libertadores");
        Decree(libLose, true, "libertadores");
        Check(finalMatch.Champion == "pal", "Fase de grupos e mata-mata se encadeiam sem intervenção");
        var runnerUp = Career("pal", new("pal", "libertadores", "x")); Continental.CreateSeason(runnerUp); Decree(runnerUp, true, "libertadores-groups");
        var rc = runnerUp.Life!.Competitions.Single(c => c.Id == "libertadores");
        for (int pass = 0; pass < 12 && rc.Champion == ""; pass++)
        {
            bool isFinal = rc.Matches.Count(m => m.Stage == rc.Stage) == 1;
            foreach (var m in rc.Matches.Where(m => m.Stage == rc.Stage && m.Fixture.HomeGoals == null).ToList())
            {
                var f = m.Fixture; bool mine = f.Home == "pal" || f.Away == "pal", home = f.Home == "pal"; bool single = m.Single;
                bool homeWins = mine ? (home == !isFinal) : m.Leg == 1;
                f.HomeGoals = homeWins ? 1 : 0; f.AwayGoals = homeWins ? 0 : (mine || single ? 1 : 0);
                if (!mine && !single && m.Leg == 2) { f.HomeGoals = 0; f.AwayGoals = 0; }
                ClubCompetitions.Complete(runnerUp, m);
            }
        }
        Check(rc.Champion != "pal" && Money(runnerUp, "Copa Libertadores: vice") == 35 * Mi, "Vice da Libertadores recebe R$ 35 mi");

        // --- Pré-Libertadores: three phases, then the groups or the Sul-Americana ---
        var pre = Career("pal", new("pal", "pre-libertadores", "Brasileirão (5º)")); Continental.CreateSeason(pre);
        var preCup = pre.Life!.Competitions.Single(c => c.Id == "pre-libertadores");
        Check(preCup.Matches.Count == 4 && preCup.StagePrizes.SequenceEqual([2 * Mi, 2_500_000, 3 * Mi]) && preCup.Rounds.SequenceEqual([2, 4, 6]), "Pré-Libertadores: três fases de jogo único com cotas de R$ 2 mi, R$ 2,5 mi e R$ 3 mi");
        Decree(pre, true, "pre-libertadores");
        Check(Money(pre, "Pré-Libertadores") == 7_500_000 && pre.Life.Competitions.Any(c => c.Id == "libertadores-groups"), "Vencer a Pré rende R$ 7,5 mi e leva à fase de grupos da Libertadores");
        var preLost = Career("pal", new("pal", "pre-libertadores", "Brasileirão (5º)")); Continental.CreateSeason(preLost);
        var pc = preLost.Life!.Competitions.Single(c => c.Id == "pre-libertadores");
        for (int stage = 0; stage < 3; stage++)
            foreach (var m in pc.Matches.Where(m => m.Stage == stage && m.Fixture.HomeGoals == null).ToList())
            {
                var f = m.Fixture; bool mine = f.Home == "pal" || f.Away == "pal", home = f.Home == "pal";
                bool homeWins = mine ? home == (stage < 2) : true; // wins two phases, loses the third
                f.HomeGoals = homeWins ? 1 : 0; f.AwayGoals = homeWins ? 0 : 1; ClubCompetitions.Complete(preLost, m);
            }
        Check(preLost.Life.Competitions.Any(c => c.Id == "sul-americana-groups") && !preLost.Life.Competitions.Any(c => c.Id == "libertadores-groups"), "Eliminado na terceira fase da Pré vai à fase de grupos da Sul-Americana");

        // --- Sul-Americana: groups, round of 16 and prizes ---
        var sula = Career("pal", new("pal", "sul-americana", "Brasileirão (7º)")); Continental.CreateSeason(sula);
        var sg = sula.Life!.Competitions.Single(c => c.Id == "sul-americana-groups");
        Check(sg.EntryPrize == 4_500_000 && sg.WinPrize == 625_000 && sg.Matches.Count == 12, "Sul-Americana: grupo com participação R$ 4,5 mi e R$ 625 mil por vitória");
        Decree(sula, true);
        var sc = sula.Life.Competitions.Single(c => c.Id == "sul-americana");
        Check(sc.Champion == "pal" && sc.Matches.Where(m => m.Fixture.Home == "pal" || m.Fixture.Away == "pal").Min(m => m.Stage) == 1, "Líder do grupo da Sul-Americana pula os playoffs e entra nas oitavas");
        Check(Money(sula, "Sul-Americana") + Money(sula, "Copa Sul-Americana") + Money(sula, "Título: Copa Sul-Americana") == 4_500_000 + 6 * 625_000 + 3 * Mi + 3_500_000 + 4 * Mi + 50 * Mi, "Campeão da Sul-Americana recebe R$ 68,75 mi (grupo, vitórias, oitavas, quartas, semifinal e título)");

        // --- Season close: berths for the next season come from the results ---
        var season = Game.New("pal", Game.Clubs2026Catalog());
        season.Life!.Competitions.Clear();
        foreach (var f in season.Fixtures) { bool mine = f.Home == "pal" || f.Away == "pal", home = f.Home == "pal"; f.HomeGoals = mine ? (home ? 1 : 0) : 0; f.AwayGoals = mine ? (home ? 0 : 1) : 0; }
        season.Round = season.SeasonRounds + 1;
        Qualification.AfterRound(season);
        Check(season.Qualification!.Clinched.Contains("g4") && season.Qualification.Clinched.Contains("g11") && season.News.Any(n => n.Contains("Matematicamente")), "Classificação matemática antecipada é detectada e notificada ao treinador");
        TransferMarket.NextSeason(season);
        var next = season.Qualification!;
        Check(next.Season == 2027 && next.History.Count == 1 && next.Berths.Any(b => b.ClubId == "pal" && b.Competition == "libertadores") && season.Life!.Competitions.Any(c => c.Id == "libertadores-groups") && season.News.Any(n => n.Contains("garantiu")), "Fim da temporada resolve as vagas, guarda a origem no histórico, notifica e cria a competição do clube");
        var down = Game.New("pal", Game.Clubs2026Catalog()); down.Life!.Competitions.Clear();
        foreach (var f in down.Fixtures) { bool mine = f.Home == "pal" || f.Away == "pal", home = f.Home == "pal"; f.HomeGoals = mine ? (home ? 0 : 1) : 0; f.AwayGoals = mine ? (home ? 1 : 0) : 0; }
        down.Round = down.SeasonRounds + 1; TransferMarket.NextSeason(down);
        Check(down.Qualification == null && !down.Life!.Competitions.Any(c => c.Id.StartsWith("libertadores") || c.Id.StartsWith("sul-americana")), "Clube rebaixado fica sem competição internacional");
    }
    static void Check2(bool value, string description) { if (!value) throw new Exception(description); }
}

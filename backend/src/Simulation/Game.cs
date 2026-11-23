using System.Text.Json;
namespace Brasileirao;

public static class Game
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // Seed demonstrativo: clubes ilustrativos, sem alegação de composição oficial da Série A 2026.
    public static readonly List<Club> Clubs = [new("pal", "Palmeiras", "PAL", "#4faa79", 82), new("fla", "Flamengo", "FLA", "#e34b56", 83), new("bot", "Botafogo", "BOT", "#eeeeee", 79), new("cor", "Corinthians", "COR", "#dddddd", 77), new("sao", "São Paulo", "SAO", "#ec5960", 78), new("san", "Santos", "SAN", "#fafafa", 74), new("flu", "Fluminense", "FLU", "#b04f70", 78), new("vas", "Vasco", "VAS", "#bbbbbb", 74), new("gre", "Grêmio", "GRE", "#59b6e9", 77), new("int", "Internacional", "INT", "#ea4f59", 79), new("cru", "Cruzeiro", "CRU", "#5a8dee", 77), new("cam", "Atlético-MG", "CAM", "#cccccc", 80), new("bah", "Bahia", "BAH", "#6ca8ed", 76), new("for", "Fortaleza", "FOR", "#ec6b63", 76), new("cea", "Ceará", "CEA", "#eeeeee", 71), new("vit", "Vitória", "VIT", "#db5960", 71), new("spo", "Sport", "SPO", "#ea525b", 70), new("juv", "Juventude", "JUV", "#77bf8a", 70), new("mir", "Mirassol", "MIR", "#e8ca6b", 70), new("rbb", "Bragantino", "RBB", "#f1f1f1", 73)];
    public static SeasonCatalog DemoCatalog() => new()
    {
        Version = "fictional-2026-v1",
        Season = 2026,
        Source = "Dados demonstrativos gerados pelo projeto. Clubes ilustrativos; atletas fictícios.",
        License = "Atletas sintéticos para testes. Nomes de clubes são referências; sem escudos ou imagens oficiais.",
        Clubs = [.. Clubs],
        Players = Clubs.SelectMany((c, i) => Players(i * 100).Select(p => new CatalogPlayer(c.Id, p)))
            .Concat(Players(10000).Take(12).Select(p => new CatalogPlayer("", p))).ToList()
    };
    public const string Clubs2026Version = "serie-a-2026-clubes-reais-v1";
    private sealed record ClubSource(int Season, string Source, List<Club> Clubs);

    public static SeasonCatalog Clubs2026Catalog()
    {
        using var stream = typeof(Game).Assembly.GetManifestResourceStream("Brasileirao.Clubs2026.json")
            ?? throw new InvalidOperationException("A lista da Série A de 2026 não foi incluída na compilação.");
        var source = JsonSerializer.Deserialize<ClubSource>(stream, Json)
            ?? throw new InvalidOperationException("A lista da Série A de 2026 está vazia.");
        return new SeasonCatalog
        {
            Version = Clubs2026Version,
            Season = source.Season,
            Source = $"Clubes da Série A de 2026 conferidos na CBF: {source.Source}",
            License = "Elencos, agentes livres e atributos simulados pelo futbrx. Escudos com créditos próprios.",
            Clubs = source.Clubs,
            Players = source.Clubs.SelectMany((club, index) =>
                Players(index * 100).Select(player => new CatalogPlayer(club.Id, player)))
                .Concat(Players(10000).Take(12).Select(player => new CatalogPlayer("", player))).ToList()
        };
    }

    public const string Candidate2026Version = "serie-a-2026-misto-candidatos-v2";
    private sealed record Candidate(int Id, string ClubId, string Name, string Position, int Age, decimal? MarketValueEur = null);
    private sealed record CandidateSource(List<Candidate> Players);

    public static SeasonCatalog Candidate2026Catalog()
    {
        var basis = Clubs2026Catalog();
        using var stream = typeof(Game).Assembly.GetManifestResourceStream("Brasileirao.Candidates2026.json")
            ?? throw new InvalidOperationException("Candidatos de elenco indisponíveis.");
        var candidates = JsonSerializer.Deserialize<CandidateSource>(stream, Json)
            ?? throw new InvalidOperationException("Lista de candidatos vazia.");
        foreach (var candidate in candidates.Players)
        {
            var slot = basis.Players.FindIndex(row => row.ClubId == candidate.ClubId
                && row.Player.Position == candidate.Position && row.Player.Id < 10000);
            if (slot < 0) continue;
            var player = basis.Players[slot].Player;
            player.Id = candidate.Id;
            player.Name = candidate.Name;
            player.Age = candidate.Age;
            // A missing market value leaves the existing estimated rating intact.
            player.Rating = MarketRating.FromEuros(candidate.MarketValueEur) ?? player.Rating;
        }
        basis.Version = Candidate2026Version;
        basis.Source = "Clubes da Série A 2026 e candidatos do Wikidata de 21/11/2026. Vínculos dos jogadores não conferidos; elencos incompletos são preenchidos com atletas fictícios.";
        basis.License = "Dados de candidatos Wikidata CC0; habilidades, valores e salários estimados. Escudos com créditos próprios.";
        return basis;
    }

    public static GameState New(string club) => New(club, DemoCatalog());
    public static GameState New(string club, SeasonCatalog catalog)
    {
        CatalogImport.Validate(catalog);
        if (!catalog.Clubs.Any(c => c.Id == club)) throw new GameRuleException("Clube ausente nesta versão de dados.");
        var copy = JsonSerializer.Deserialize<SeasonCatalog>(JsonSerializer.Serialize(catalog, Json), Json)!;
        var g = new GameState { LotterySeed = Guid.NewGuid().ToString("N"), ClubId = club, DataVersion = copy.Version, Season = copy.Season, Clubs = copy.Clubs };
        foreach (var c in copy.Clubs)
        {
            var squad = copy.Players.Where(p => p.ClubId == c.Id).Select(p => p.Player).ToList();
            foreach (var p in squad) p.Starter = false;
            var starters = squad.Where(p => p.Position == "GOL").Take(1)
                .Concat(squad.Where(p => p.Position == "ZAG").Take(2))
                .Concat(squad.Where(p => p.Position == "LAT").Take(2))
                .Concat(squad.Where(p => p.Position == "MEI").Take(3))
                .Concat(squad.Where(p => p.Position == "ATA").Take(3)).ToList();
            starters.AddRange(squad.Where(p => p.Position != "GOL" && !starters.Contains(p)).Take(11 - starters.Count));
            foreach (var p in starters) p.Starter = true;
            if (c.Id == club) g.Squad = squad;
            else g.OpponentSquads[c.Id] = squad;
        }
        g.Market = copy.Players.Where(p => p.ClubId == "").Select(p => p.Player).ToList();
        foreach (var p in g.Market) p.Starter = false;
        g.Fixtures = SeasonCalendar.Generate(g.Clubs, g.Season, g.LotterySeed);
        g.CalendarSource = SeasonCalendar.Source; g.CalendarVersion = 2;
        TransferMarket.Upgrade(g);
        Sports.Upgrade(g);
        Youth.Upgrade(g);
        Training.Upgrade(g);
        ClubLife.Upgrade(g);
        return g;
    }
    public static List<Player> Players(int offset) { string[] first = ["Rafael", "Lucas", "Gabriel", "Matheus", "Pedro", "André", "Bruno", "Caio", "Diego", "Felipe", "Igor", "João", "Luan", "Marcos", "Nicolas", "Otávio", "Paulo", "Renan", "Samuel", "Thiago", "Vitor", "Wesley", "Davi"]; string[] last = ["Almeida", "Costa", "Silveira", "Mendes", "Rocha", "Barros", "Lima"]; return Enumerable.Range(0, 23).Select(i => new Player { Id = i + offset, Name = first[(i + offset) % first.Length] + " " + last[(i * 3 + offset) % last.Length], Position = i < 2 ? "GOL" : i < 6 ? "ZAG" : i < 10 ? "LAT" : i < 16 ? "MEI" : "ATA", Age = 18 + (i * 7 + offset) % 17, Rating = 65 + (i * 7 + offset) % 21, Value = (3 + (i * 7) % 20) * 1000000, Wage = (30 + i * 4) * 1000, Starter = new[] { 0, 2, 3, 6, 7, 10, 11, 12, 16, 17, 18 }.Contains(i) }).ToList(); }
    public static List<Fixture> Schedule(List<Club> clubs) => SeasonCalendar.Generate(clubs, 2026, Guid.NewGuid().ToString("N"));
    public static object[] Table(GameState g) => LeagueRules.Table(g.Clubs, g.Fixtures, g.LotterySeed + ":" + g.Season).Select(r => new { club = r.Club, played = r.Played, wins = r.Wins, draws = r.Draws, losses = r.Losses, goals = r.Goals, against = r.Against, difference = r.Difference, points = r.Points, redCards = r.RedCards, yellowCards = r.YellowCards }).Cast<object>().ToArray();
    public static void Advance(GameState g)
    {
        ClubLife.Upgrade(g);ClubCompetitions.Advance(g,true);
        var startingBalance = g.Balance;
        PrepareRound(g);
        CompleteRound(g, startingBalance);
    }
    public static void PrepareRound(GameState g)
    {
        TransferMarket.Upgrade(g);
        if (g.Round > 38) throw new GameRuleException("Temporada encerrada. Inicie a próxima temporada.");
        AdvancedMarket.Settle(g);
        ClubLife.BeforeRound(g);
        Training.BeforeRound(g);
        ClubLife.Travel(g,g.Fixtures.Single(f=>f.Round==g.Round&&(f.Home==g.ClubId||f.Away==g.ClubId)));
        Sports.Prepare(g);
    }
    public static void CompleteRound(GameState g, long? originalBalance = null)
    {
        long startingBalance = originalBalance ?? g.Balance;
        foreach (var f in g.Fixtures.Where(f => f.Round == g.Round && f.HomeGoals == null)) Simulate(g, f);
        Divisions.Advance(g);
        Sports.AfterRound(g, g.Fixtures.Concat(g.Divisions?.OtherFixtures ?? []).Where(f => f.Round == g.Round).ToList());
        ClubLife.AfterRound(g);
        ClubCompetitions.Advance(g);
        CoachCareer.SettleRound(g);
        CoachCareer.ProcessCompleted(g);
        g.Round++;
        TransferMarket.AfterRound(g);
        Youth.AfterRound(g);
        g.News.Insert(0, $"Rodada {g.Round - 1} concluída. Saldo da semana: R$ {g.Balance - startingBalance:N0}.");
    }
    public static void Simulate(GameState g, Fixture f)
    {
        f.Setup ??= new(MatchSimulation.Version,
            Sports.BuildTeam(g, f.Home), Sports.BuildTeam(g, f.Away));
        var result = MatchSimulation.Run(f.Setup, f.Seed, false);
        ApplyResult(f, result);
    }
    public static void ApplyResult(Fixture f, MatchReplay result)
    {
        f.HomePenalties=result.HomePenalties;f.AwayPenalties=result.AwayPenalties;
        f.HomeGoals = result.HomeGoals; f.AwayGoals = result.AwayGoals;
        f.PlayerStats = result.PlayerStats ?? [];
        // Passes são regenerados no replay; o save guarda apenas os lances decisivos.
        f.Events = result.Events.Where(e => e.Type is not ("pass" or "interception")).ToList();
    }
    public static GameState Read(Career c) { var state = JsonSerializer.Deserialize<GameState>(c.State, Json)!; TransferMarket.Upgrade(state); Sports.Upgrade(state); Youth.Upgrade(state); Training.Upgrade(state); SeasonCalendar.Upgrade(state); ClubLife.Upgrade(state); return state; }
    public static void Write(Career c, GameState g) { c.State = JsonSerializer.Serialize(g, Json); c.Revision++; c.Updated = DateTime.UtcNow; }
}





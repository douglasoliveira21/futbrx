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
                Players(index * 100, club.Strength).Select(player => new CatalogPlayer(club.Id, player)))
                .Concat(Players(10000).Take(12).Select(player => new CatalogPlayer("", player))).ToList()
        };
    }

    public const string SerieB2026Version = "serie-b-2026-clubes-reais-v1";
    public const string SerieC2026Version = "serie-c-2026-clubes-reais-v1";
    // Série D has one data version per group of six clubs: "serie-d-2026-grupo-01-v1" to "-16-v1".
    public const string SerieDVersionPrefix = "serie-d-2026-grupo-";
    private sealed record SeriesClub(string Id, string Series, string Name, string Short, string Color, int Strength);
    private sealed record SeriesSource(List<SeriesClub> Clubs);

    // The lower series move less money: starting cash and the cash of the other clubs scale with it.
    public static double EconomyFactor(string version) =>
        version.StartsWith("serie-b-2026", StringComparison.Ordinal) ? .45
        : version.StartsWith("serie-c-2026", StringComparison.Ordinal) ? .2
        : version.StartsWith(SerieDVersionPrefix, StringComparison.Ordinal) ? .1 : 1;

    // Real clubs of Série B or C 2026 (20 each), from the researched directory in series-2026.json.
    public static SeasonCatalog SeriesCatalog(string series)
    {
        using var stream = typeof(Game).Assembly.GetManifestResourceStream("Brasileirao.Series2026.json")
            ?? throw new InvalidOperationException("O diretório de clubes por série não foi incluído na compilação.");
        var source = JsonSerializer.Deserialize<SeriesSource>(stream, Json)
            ?? throw new InvalidOperationException("O diretório de clubes por série está vazio.");
        var clubs = source.Clubs.Where(c => c.Series == series)
            .Select(c => new Club(c.Id, c.Name, c.Short, c.Color, c.Strength)).ToList();
        if (series is not ("B" or "C") || clubs.Count != 20)
            throw new InvalidOperationException($"A Série {series} precisa de vinte clubes no diretório.");
        return new SeasonCatalog
        {
            Version = series == "B" ? SerieB2026Version : SerieC2026Version,
            Season = 2026,
            Source = $"Clubes da Série {series} de 2026 conforme a tabela de participantes da Wikipédia (fatos públicos), pesquisados em 23/11/2026. Siglas, cores e forças são estimativas do projeto, ainda a conferir.",
            License = "Elencos, agentes livres e atributos simulados pelo futbrx. Escudos genéricos, sem imagens oficiais.",
            Clubs = clubs,
            Players = clubs.SelectMany((club, index) =>
                Players(index * 100, club.Strength).Select(player => new CatalogPlayer(club.Id, player)))
                .Concat(Players(10000).Take(12).Select(player => new CatalogPlayer("", player))).ToList()
        };
    }

    // All 156 clubs of Séries A-D with state and city, plus which data versions each series can start in.
    static readonly Lazy<string> directory = new(() =>
    {
        using var stream = typeof(Game).Assembly.GetManifestResourceStream("Brasileirao.Series2026.json")
            ?? throw new InvalidOperationException("O diretório de clubes por série não foi incluído na compilação.");
        var node = System.Text.Json.Nodes.JsonNode.Parse(stream)!.AsObject();
        node["versions"] = new System.Text.Json.Nodes.JsonObject
        {
            ["A"] = new System.Text.Json.Nodes.JsonArray(Candidate2026Version, Clubs2026Version),
            ["B"] = new System.Text.Json.Nodes.JsonArray(SerieB2026Version),
            ["C"] = new System.Text.Json.Nodes.JsonArray(SerieC2026Version),
            ["D"] = new System.Text.Json.Nodes.JsonArray(SerieD.Versions().Select(v => (System.Text.Json.Nodes.JsonNode?)v).ToArray()),
        };
        // Every Série D club starts in the data version of its group.
        foreach (var club in node["clubs"]!.AsArray().OfType<System.Text.Json.Nodes.JsonObject>().Where(c => c["series"]!.GetValue<string>() == "D"))
        {
            var (group, version) = SerieD.Entry(club["id"]!.GetValue<string>());
            club["group"] = group; club["version"] = version;
        }
        return node.ToJsonString();
    });
    public static string DirectoryJson() => directory.Value;

    // Série jogável de cada versão de dados conhecida; versões importadas pelo administrador não pertencem a uma série.
    public static string? SeriesOfVersion(string version) =>
        version is Candidate2026Version or Clubs2026Version ? "A" : version == SerieB2026Version ? "B" : version == SerieC2026Version ? "C" : null;

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
        g.Balance = (long)(g.Balance * EconomyFactor(copy.Version));
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
        if (SerieD.IsVersion(copy.Version)) { g.SerieD = SerieD.Create(copy); g.SeasonRounds = SerieD.SeasonRounds; }
        g.SeasonRounds = SeriesFormats.SeasonRoundsFor(copy.Version);
        g.Fixtures = SeasonCalendar.Generate(g.Clubs, g.Season, g.LotterySeed, "A", 1, SeriesFormats.SingleRound(g));
        g.CalendarSource = SeasonCalendar.Source; g.CalendarVersion = 2;
        TransferMarket.Upgrade(g);
        Sports.Upgrade(g);
        Youth.Upgrade(g);
        Training.Upgrade(g);
        ClubLife.Upgrade(g);
        return g;
    }
    // Squad of a club of the given strength: the same generated players, shifted so that a club of strength 70 (Série A) keeps
    // them as they were and weaker clubs (Séries B to D) field weaker players.
    public static List<Player> Players(int offset, int strength) { var list = Players(offset); int shift = strength - 70; if (shift != 0) foreach (var p in list) p.Rating = Math.Clamp(p.Rating + shift, 30, 99); return list; }
    public static List<Player> Players(int offset) { string[] first = ["Rafael", "Lucas", "Gabriel", "Matheus", "Pedro", "André", "Bruno", "Caio", "Diego", "Felipe", "Igor", "João", "Luan", "Marcos", "Nicolas", "Otávio", "Paulo", "Renan", "Samuel", "Thiago", "Vitor", "Wesley", "Davi"]; string[] last = ["Almeida", "Costa", "Silveira", "Mendes", "Rocha", "Barros", "Lima"]; return Enumerable.Range(0, 23).Select(i => new Player { Id = i + offset, Name = first[(i + offset) % first.Length] + " " + last[(i * 3 + offset) % last.Length], Position = i < 2 ? "GOL" : i < 6 ? "ZAG" : i < 10 ? "LAT" : i < 16 ? "MEI" : "ATA", Age = 18 + (i * 7 + offset) % 17, Rating = 65 + (i * 7 + offset) % 21, Value = (3 + (i * 7) % 20) * 1000000, Wage = (30 + i * 4) * 1000, Starter = new[] { 0, 2, 3, 6, 7, 10, 11, 12, 16, 17, 18 }.Contains(i) }).ToList(); }
    public static List<Fixture> Schedule(List<Club> clubs) => SeasonCalendar.Generate(clubs, 2026, Guid.NewGuid().ToString("N"));
    public static object[] Table(GameState g) => LeagueRules.Table(g.Clubs, g.Fixtures, g.LotterySeed + ":" + g.Season).Select(r => new { club = r.Club, played = r.Played, wins = r.Wins, draws = r.Draws, losses = r.Losses, goals = r.Goals, against = r.Against, difference = r.Difference, points = r.Points, redCards = r.RedCards, yellowCards = r.YellowCards }).Cast<object>().ToArray();
    // The club has a league match in the current round (cup matches are checked apart, by ClubCompetitions.NextDue).
    public static bool HasLeagueMatch(GameState g) => g.Fixtures.Any(f => f.Round == g.Round && (f.Home == g.ClubId || f.Away == g.ClubId));
    // A week without a match for the club (state championship, a league that has not started, a bye): the
    // week passes without a broadcast; every other match of the week is still played.
    public static void AdvanceWeek(GameState g)
    {
        LiveBroadcast.EnsureIdle(g);
        ClubLife.Upgrade(g);
        if (g.Round > g.SeasonRounds) throw new GameRuleException("Temporada encerrada. Inicie a próxima temporada.");
        if (ClubCompetitions.NextDue(g) != null || HasLeagueMatch(g)) throw new GameRuleException("Há partida do seu clube nesta semana. Entre em campo.");
        Advance(g);
    }
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
        if (g.Round > g.SeasonRounds) throw new GameRuleException("Temporada encerrada. Inicie a próxima temporada.");
        AdvancedMarket.Settle(g);
        ClubLife.BeforeRound(g);
        Training.BeforeRound(g);
        var own=g.Fixtures.SingleOrDefault(f=>f.Round==g.Round&&(f.Home==g.ClubId||f.Away==g.ClubId));if(own!=null)ClubLife.Travel(g,own);
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
        SerieD.AfterRound(g);
        SeriesFormats.AfterRound(g);
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





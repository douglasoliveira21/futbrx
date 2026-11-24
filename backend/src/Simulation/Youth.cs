namespace Brasileirao;

public static class Youth
{
    public const int Capacity = 18;
    public const long SearchFee = 150000, RecruitFee = 50000, PromotionFee = 100000, WeeklyCost = 6000;
    public static readonly string[] Regions = ["Sudeste", "Sul", "Nordeste", "Centro-Oeste", "Norte"];
    public static readonly string[] Positions = ["GOL", "ZAG", "LAT", "MEI", "ATA"];
    static int Seed(GameState g, int salt) => unchecked(g.Season * 10007 + g.Clubs.FindIndex(c => c.Id == g.ClubId) * 701 + salt);
    static int NextId(GameState g)
    {
        int max = g.Squad.Concat(g.Market).Concat(g.Academy).Concat(g.OpponentSquads.Values.SelectMany(p => p)).Select(p => p.Id).Concat(g.Contracts.Keys).Concat(g.Development.Keys).Concat(g.YouthProspects.Select(p => p.Player.Id)).DefaultIfEmpty(0).Max();
        g.NextYouthPlayerId = Math.Max(g.NextYouthPlayerId, max + 1); return g.NextYouthPlayerId++;
    }
    static Player Generate(GameState g, Random rng, string? position = null)
    {
        int id = NextId(g); var p = Game.Players(rng.Next(0, 1000))[rng.Next(23)];
        p.Id = id; p.Position = position ?? Positions[rng.Next(Positions.Length)]; p.Age = rng.Next(16, 20); p.Rating = rng.Next(43, 66); p.Value = (p.Rating - 35) * 100000; p.Wage = (p.Rating - 35) * 1000; p.Starter = false; p.TransferListed = false;
        return p;
    }
    public static Player GenerateForCategory(GameState g,string category,int sequence){var rng=new Random(ClubLife.Seed(g,$"trial:{g.Season}:{g.Round}:{category}:{sequence}"));var p=Generate(g,rng);p.Age=category=="Sub-15"?rng.Next(12,15):category=="Sub-17"?rng.Next(15,17):rng.Next(17,20);return p;}
    static void Log(GameState g, string text) { g.YouthHistory.Insert(0, $"{g.Season} · rodada {g.Round}: {text}"); if (g.YouthHistory.Count > 50) g.YouthHistory.RemoveRange(50, g.YouthHistory.Count - 50); g.News.Insert(0, text); }
    public static void Upgrade(GameState g)
    {
        if (g.YouthVersion >= 1) return;
        var rng = new Random(Seed(g, 0)); foreach (var position in new[] { "GOL", "ZAG", "LAT", "MEI", "ATA", "MEI" }) g.Academy.Add(Generate(g, rng, position)); g.YouthVersion = 1;
    }
    public static void StartSearch(GameState g, string region, string position)
    {
        if (!Regions.Contains(region) || position != "Todas" && !Positions.Contains(position)) throw new GameRuleException("Escolha região e posição válidas.");
        if (g.Round > 36) throw new GameRuleException("Inicie observações até a rodada 36 para receber o relatório nesta temporada.");
        if (g.Scouting?.Status == "searching") throw new GameRuleException("Já existe uma observação em andamento.");
        if (g.Balance < SearchFee) throw new GameRuleException("Saldo insuficiente para a observação.");
        g.Balance -= SearchFee; g.Ledger.Add(new(g.Round, "Observação de jovens: " + region, -SearchFee, g.Season));
        g.ScoutingSequence++; g.Scouting = new() { Season = g.Season, StartedRound = g.Round, CompletionRound = g.Round + 2, Region = region, Position = position, Seed = Seed(g, g.ScoutingSequence * 101 + Array.IndexOf(Regions, region) * 37 + Array.IndexOf(Positions, position) * 13) };
        g.YouthProspects.Clear(); Log(g, $"Observação iniciada no {region}. Relatório previsto após duas rodadas.");
    }
    public static void Recruit(GameState g, int id)
    {
        var candidate = g.YouthProspects.Find(p => p.Player.Id == id) ?? throw new GameRuleException("Jovem não está no relatório atual.");
        if (g.Scouting?.Status != "completed" || g.Scouting.Season != g.Season || g.Round > g.Scouting.ExpiresRound) throw new GameRuleException("Relatório de observação expirado.");
        if (g.Academy.Count >= Capacity) throw new GameRuleException("A base atingiu o limite de dezoito atletas. Promova jovens para abrir vagas.");
        if (g.Balance < candidate.SigningFee) throw new GameRuleException("Saldo insuficiente para incorporar o jovem.");
        g.Balance -= candidate.SigningFee; g.Ledger.Add(new(g.Round, "Captação: " + candidate.Player.Name, -candidate.SigningFee, g.Season));
        g.YouthProspects.Remove(candidate); g.Academy.Add(candidate.Player); Training.Upgrade(g); Log(g, $"{candidate.Player.Name} integrou as categorias de base.");
    }
    public static void Promote(GameState g, int id)
    {
        var p = g.Academy.Find(p => p.Id == id) ?? throw new GameRuleException("Atleta não pertence à base do clube.");
        if(p.Age<16)throw new GameRuleException("A promoção ao profissional nesta simulação exige dezesseis anos.");
        if (g.Squad.Count >= 40) throw new GameRuleException("O profissional atingiu o limite de quarenta atletas.");
        if (g.Balance < PromotionFee) throw new GameRuleException("Saldo insuficiente para as luvas de promoção.");
        g.Balance -= PromotionFee; g.Ledger.Add(new(g.Round, "Luvas de promoção: " + p.Name, -PromotionFee, g.Season));
        g.Academy.Remove(p); p.Starter = false; g.Squad.Add(p);
        var contract = new PlayerContract { PlayerId = id, ClubId = g.ClubId, StartSeason = g.Season, StartRound = g.Round, EndSeason = g.Season + 2, Wage = p.Wage, SigningBonus = PromotionFee }; g.Contracts[id] = contract;
        g.Transfers.Insert(0, new(g.Season, g.Round, id, p.Name, g.ClubId, g.ClubId, 0, PromotionFee, p.Wage, contract.EndSeason, "academy")); Sports.RefreshBench(g);
        Log(g, $"{p.Name} foi promovido ao profissional, com vínculo até o fim de {contract.EndSeason}.");
    }
    public static void AfterRound(GameState g)
    {
        long cost = g.Academy.Count * WeeklyCost; g.Balance -= cost; if (cost > 0) g.Ledger.Add(new(g.Round - 1, "Manutenção das categorias de base", -cost, g.Season));
        if (g.Scouting is { Status: "searching" } mission && g.Round >= mission.CompletionRound)
        {
            var rng = new Random(mission.Seed); g.YouthProspects = Enumerable.Range(0, 6).Select(_ => new YouthProspect(Generate(g, rng, mission.Position == "Todas" ? null : mission.Position), mission.Region, RecruitFee)).ToList();
            mission.Status = "completed"; mission.ExpiresRound = Math.Min(g.SeasonRounds + 1, g.Round + 4); Log(g, $"Relatório de observação disponível: seis jovens do {mission.Region}. Válido até a rodada {mission.ExpiresRound}.");
        }
        if (g.Scouting is { Status: "completed" } completed && g.Round > completed.ExpiresRound) { completed.Status = "expired"; g.YouthProspects.Clear(); }
    }
    public static void NewSeason(GameState g)
    {
        g.Scouting = null; g.YouthProspects.Clear();
        foreach (var p in g.Academy.ToArray())
        {
            p.Age++; p.Fitness = 100;
            if (p.Age > 20) { g.Academy.Remove(p); p.Starter = false; g.Market.Add(p); Log(g, $"{p.Name} completou 21 anos e deixou a base como agente livre. Não houve receita de transferência."); }
        }
        var rng = new Random(Seed(g, 301)); for (int i = 0, count = Math.Min(3, Capacity - g.Academy.Count); i < count; i++) g.Academy.Add(Generate(g, rng));
        Training.Upgrade(g);
    }
}

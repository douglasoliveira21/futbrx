namespace Brasileirao;

public static class Divisions
{
    public static void EnableDemo(GameState g)
    {
        if (g.Divisions != null || g.Round != 1 || g.Fixtures.Any(f => f.HomeGoals != null)) throw new GameRuleException("Ative as divisões antes da primeira rodada, em carreira sem divisões.");
        if (!g.DataVersion.StartsWith("fictional-", StringComparison.Ordinal)) throw new GameRuleException("A expansão demonstrativa usa clubes e atletas fictícios e não pode ser misturada a uma base real.");
        var clubs = Enumerable.Range(1, 20).Select(i => new Club("demo-b-" + i, "Associação Regional " + i.ToString("00"), "R" + i.ToString("00"), "#6699aa", 60 + i % 12)).ToList();
        int offset = Math.Max(700_000, g.Squad.Concat(g.Market).Concat(g.OpponentSquads.Values.SelectMany(s => s)).Max(p => p.Id) + 100);
        foreach (var (club, i) in clubs.Select((c, i) => (c, i)))
        {
            var squad = Game.Players(offset + i * 100); g.OpponentSquads[club.Id] = squad; g.ClubBalances[club.Id] = 30_000_000;
            foreach (var p in squad) g.Contracts[p.Id] = new() { PlayerId = p.Id, ClubId = club.Id, StartSeason = g.Season, StartRound = 1, EndSeason = g.Season + 3, Wage = p.Wage };
        }
        g.Divisions = new() { OtherClubs = clubs, OtherFixtures = SeasonCalendar.Generate(clubs, g.Season, g.LotterySeed, "B") };
        g.News.Insert(0, "Duas divisões ativadas com clubes regionais fictícios. Série B: dois acessos diretos e playoffs de ida e volta entre 3º/6º e 4º/5º. Modelo 2026 mantido nas temporadas seguintes desta carreira.");
    }
    public static void Advance(GameState g)
    {
        if (g.Divisions == null) return;
        foreach (var f in g.Divisions.OtherFixtures.Where(f => f.Round == g.Round)) Game.Simulate(g, f);
    }
    public static void Complete(GameState g)
    {
        var d = g.Divisions; if (d == null || d.Promoted.Count > 0) return;
        if (g.Round <= 38 || d.OtherFixtures.Any(f => f.HomeGoals == null)) throw new GameRuleException("Conclua ambas as divisões antes de definir o acesso.");
        Sports.Prepare(g);
        bool managedA = d.ManagedDivision == "A";
        var a = LeagueRules.Table(managedA ? g.Clubs : d.OtherClubs, managedA ? g.Fixtures : d.OtherFixtures, g.LotterySeed + g.Season);
        var b = LeagueRules.Table(managedA ? d.OtherClubs : g.Clubs, managedA ? d.OtherFixtures : g.Fixtures, g.LotterySeed + g.Season);
        d.Relegated = a.TakeLast(4).Select(r => r.Club.Id).ToList(); d.Promoted = b.Take(2).Select(r => r.Club.Id).ToList();
        foreach (var p in g.Squad.Concat(g.OpponentSquads.Values.SelectMany(s => s))) p.YellowCards = 0;
        foreach (var (high, low) in new[] { (b[2].Club.Id, b[5].Club.Id), (b[3].Club.Id, b[4].Club.Id) })
        {
            var first = new Fixture { Round = 39, Home = low, Away = high, Seed = Seed(g, low, 1) };
            var second = new Fixture { Round = 40, Home = high, Away = low, Seed = Seed(g, high, 2) };
            SeasonCalendar.AssignDates([first, second], g.Season, g.LotterySeed, "playoff-" + high, false);
            Game.Simulate(g, first); Sports.AfterRound(g, [first], true); Sports.Prepare(g);
            Game.Simulate(g, second); Sports.AfterRound(g, [second], true); d.Playoffs.AddRange([first, second]);
            int highGoals = first.AwayGoals!.Value + second.HomeGoals!.Value, lowGoals = first.HomeGoals!.Value + second.AwayGoals!.Value;
            int highPoints = new[] { (first.AwayGoals.Value, first.HomeGoals.Value), (second.HomeGoals.Value, second.AwayGoals.Value) }.Sum(s => s.Item1 > s.Item2 ? 3 : s.Item1 == s.Item2 ? 1 : 0);
            int lowPoints = new[] { (first.HomeGoals.Value, first.AwayGoals.Value), (second.AwayGoals.Value, second.HomeGoals.Value) }.Sum(s => s.Item1 > s.Item2 ? 3 : s.Item1 == s.Item2 ? 1 : 0);
            string winner = highPoints != lowPoints ? (highPoints > lowPoints ? high : low) : highGoals >= lowGoals ? high : low;
            if (highPoints == lowPoints && highGoals == lowGoals) second.Events.Add(new(90, "playoff_tiebreak", high, 0, "Empate em pontos e saldo: acesso para a melhor campanha da fase regular."));
            d.Promoted.Add(winner);
        }
        g.News.Insert(0, "Acesso e descenso definidos. Consulte os playoffs e os quatro promovidos na tela de competições.");
    }
    static int Seed(GameState g, string club, int leg) => BitConverter.ToInt32(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(g.LotterySeed + ":" + g.Season + ":" + club + ":" + leg)), 0);
    public static void NextSeason(GameState g)
    {
        var d = g.Divisions; if (d == null) return; Complete(g);
        var all = g.Clubs.Concat(d.OtherClubs).ToList();
        var oldA = d.ManagedDivision == "A" ? g.Clubs : d.OtherClubs;
        var oldB = d.ManagedDivision == "A" ? d.OtherClubs : g.Clubs;
        var a = oldA.Where(c => !d.Relegated.Contains(c.Id)).Concat(all.Where(c => d.Promoted.Contains(c.Id))).ToList();
        var b = oldB.Where(c => !d.Promoted.Contains(c.Id)).Concat(all.Where(c => d.Relegated.Contains(c.Id))).ToList();
        bool staysA = a.Any(c => c.Id == g.ClubId); g.Clubs = staysA ? a : b;
        g.Divisions = new() { ManagedDivision = staysA ? "A" : "B", OtherClubs = staysA ? b : a, OtherFixtures = SeasonCalendar.Generate(staysA ? b : a, g.Season + 1, g.LotterySeed, staysA ? "B" : "A") };
    }
}

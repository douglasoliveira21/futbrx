using Brasileirao;
using System.Text.Json;

public static class FootballTests
{
    static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    public static void Run()
    {
        var game = Game.New("pal");
        var setup = new MatchSetup(MatchSimulation.Version, Sports.BuildTeam(game, "pal"), Sports.BuildTeam(game, "fla"));
        var actions = new HashSet<string>(); int goals = 0, penalties = 0;
        for (int seed = 1; seed <= 8; seed++)
        {
            var replay = MatchSimulation.Run(setup, seed, true);
            var summary = MatchSimulation.Run(setup, seed, false);
            goals += replay.HomeGoals + replay.AwayGoals; penalties += replay.Events.Count(e => e.Type == "penalty");
            Check(replay.HomeGoals == summary.HomeGoals && replay.AwayGoals == summary.AwayGoals && JsonSerializer.Serialize(replay.Events) == JsonSerializer.Serialize(summary.Events), $"motor de futebol resumo/replay consistentes, seed {seed}");
            foreach (var frame in replay.Frames)
                foreach (var p in frame.Players)
                {
                    actions.Add(p.Action);
                    if (p.Action is "pass" or "cross" or "header" or "shot")
                        CheckTime(p.ActionSecond >= 0 && p.ActionSecond <= frame.Second);
                }
            Check(replay.Frames.Any(f => f.Ball.Y > 1.8), "motor de futebol inclui bola aérea");
            Check(replay.Frames.Zip(replay.Frames.Skip(1)).All(f => f.First.Second < f.Second.Second), "motor de futebol timestamps únicos e crescentes");
        }
        Check(goals / 8.0 < 7 && penalties / 8.0 < 3, $"Distribuição de teste sem inflação de pênaltis: {goals / 8.0:F2} gols e {penalties / 8.0:F2} pênaltis por partida");
        foreach (var action in new[] { "pass", "cross", "header", "shot", "dribble", "keeper_ready", "catch", "dive_left", "dive_right" })
            Check(actions.Contains(action), "motor de futebol produz ação específica: " + action);
        var oldV4 = setup with { EngineVersion = PositionalV4.Version };
        Check(JsonSerializer.Serialize(MatchSimulation.Run(oldV4, 42, true)) == JsonSerializer.Serialize(PositionalV4.Run(oldV4, 42, true)), "Dispatcher conserva replay v4");
        var oldV3 = setup with { EngineVersion = PositionalV3.Version };
        Check(JsonSerializer.Serialize(MatchSimulation.Run(oldV3, 42, true)) == JsonSerializer.Serialize(PositionalV3.Run(oldV3, 42, true)), "Dispatcher conserva replay v3");
        var legacy = setup with { EngineVersion = PositionalV2.Version };
        Check(JsonSerializer.Serialize(MatchSimulation.Run(legacy, 42, true)) == JsonSerializer.Serialize(PositionalV2.Run(legacy, 42, true)), "Dispatcher conserva replay v2");
        var current = MatchSimulation.Run(setup, 42, true);
        Check(JsonSerializer.Serialize(current) == JsonSerializer.Serialize(MatchSimulation.Run(setup, 42, true)), "motor de futebol reproduz frames e ações deterministicamente");
    }
    static void CheckTime(bool valid) { if (!valid) throw new Exception("Timestamp de contato inválido"); }
}

using Brasileirao;
using System.Text.Json;

public static class BroadcastTests
{
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    static void Reject(Action action, string message) { try { action(); } catch (GameRuleException) { Check(true, message); return; } throw new Exception(message); }
    public static void Run()
    {
        var g = Game.New("pal"); var before = JsonSerializer.Serialize(g.Fixtures, Game.Json); var balance = g.Balance;
        var now = DateTimeOffset.Parse("2026-11-21T12:00:00Z");
        var live = LiveBroadcast.Start(g, now);
        live.Fixture.Setup=live.Fixture.Setup! with{EngineVersion=PositionalV6.Version};
        Check(g.Round == 1 && g.Balance == balance && JsonSerializer.Serialize(g.Fixtures, Game.Json) == before, "Início da transmissão não antecipa placares, finanças ou rodada");
        Check(LiveBroadcast.Start(g, now).Id == live.Id, "Iniciar novamente retoma a mesma partida");
        Reject(() => LiveBroadcast.EnsureIdle(g), "Alterações de carreira bloqueadas durante a partida");
        Reject(() => LiveBroadcast.Finish(g, live.Id), "Não permite pular diretamente para o resultado");
        var publicData = JsonSerializer.Serialize(LiveBroadcast.Public(live), Game.Json);
        Check(!publicData.Contains("preparedState") && !publicData.Contains("playerStats") && publicData.Contains("\"homeGoals\":null"), "Projeção pública não expõe estado futuro nem placar final");
        LiveBroadcast.Tick(live, now, true);
        LiveBroadcast.Tick(live, now.AddSeconds(1), true);
        LiveBroadcast.Tick(live, now.AddSeconds(1), true);
        Check(live.WatchedSeconds == 1, "Duas abas não aceleram o relógio compartilhado");
        LiveBroadcast.Tick(live, now.AddHours(2), true);
        Check(live.WatchedSeconds == 1, "Tempo offline não adianta a transmissão");
        LiveBroadcast.Tick(live, now.AddHours(2).AddSeconds(1), false);
        LiveBroadcast.Tick(live, now.AddHours(2).AddSeconds(2), false);
        Check(live.WatchedSeconds == 1, "Pausa não acumula tempo assistido");
        using (var session = new LiveSimulation())
        {
            var copy = JsonSerializer.Deserialize<LiveMatch>(JsonSerializer.Serialize(live, Game.Json), Game.Json)!;
            copy.WatchedSeconds = LiveBroadcast.EntranceSeconds + 10;
            var partial = session.Advance(copy);
            Check(copy.SimulatedSecond == 120 && !copy.EngineFinished && partial.Frames.All(f => f.Second <= 120) && partial.Events.All(e => e.Second <= 120) && !partial.Events.Any(e => e.Type == "fulltime"), "Motor calcula somente os primeiros 120 segundos quando solicitado");
            using var rebuilt = new LiveSimulation();
            Check(JsonSerializer.Serialize(partial, Game.Json) == JsonSerializer.Serialize(rebuilt.Advance(copy), Game.Json), "Reinício reconstrói somente o passado e reproduz o mesmo estado parcial");
            copy.WatchedSeconds += 10; var later = session.Advance(copy);
            Check(later.Frames.Count > partial.Frames.Count && partial.Frames.All(f => f.Second <= 120), "Novos ticks acrescentam decisões sem alterar o snapshot entregue anteriormente");
        }
        var restored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(g, Game.Json), Game.Json)!;
        Check(restored.LiveMatch!.Id == live.Id && restored.LiveMatch.WatchedSeconds == 1, "Sessão retoma no ponto salvo após recarregar");
        using var runtime = new LiveSimulation();
        now = now.AddHours(2).AddSeconds(3); LiveBroadcast.Tick(live, now, true);
        for (var i = 0; !LiveBroadcast.Complete(live) && i < 1000; i++) { now = now.AddSeconds(1); LiveBroadcast.Tick(live, now, true); runtime.Advance(live); }
        Check(LiveBroadcast.MatchSecond(live) == live.Duration, "Acompanhamento contínuo cobre todos os segundos até os acréscimos");
        live.WatchedSeconds = LiveBroadcast.EntranceSeconds + live.Duration / (double)LiveBroadcast.Speed;
        var exactFinal = runtime.Advance(live);
        Check(live.EngineFinished && LiveBroadcast.Complete(live) && exactFinal.Frames[^1].Second == live.Duration && LiveBroadcast.MatchSecond(live) == live.Duration, "Relógio no limite exato mantém apito final e último frame sem erro de arredondamento");
        Reject(() => LiveBroadcast.Finish(g, Guid.NewGuid()), "Identificador errado não finaliza a partida");
        var finished = LiveBroadcast.Finish(g, live.Id);
        var fixture = finished.Fixtures.Single(f => f.Round == 1 && (f.Home == g.ClubId || f.Away == g.ClubId));
        Check(finished.Round == 2 && finished.LiveMatch == null && finished.Fixtures.Count(f => f.HomeGoals != null) == 10, "Apito final aplica a rodada inteira uma única vez");
        var replay = MatchSimulation.Run(fixture.Setup!, fixture.Seed, true);
        Check(replay.HomeGoals == fixture.HomeGoals && replay.AwayGoals == fixture.AwayGoals, "Resultado salvo reproduz exatamente a transmissão");
    }
}

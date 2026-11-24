namespace Brasileirao;

// In-memory coroutine; rebuilding it consumes only the persisted past, never the rest of the match.
public sealed class LiveSimulation : IDisposable
{
    IEnumerator<PositionalV5.Checkpoint>? engine;
    PositionalV5.Checkpoint? checkpoint;
    readonly object gate = new();
    static readonly HashSet<string> Stoppages = ["throw_in", "goal_kick", "corner", "offside", "free_kick", "goal", "penalty", "halftime"];
    int instructionCount = -1;
    public MatchReplay Advance(LiveMatch live)
    {
        lock (gate)
        {
            var target = (int)Math.Floor((Math.Max(0, (live.WatchedSeconds - LiveBroadcast.EntranceSeconds) * LiveBroadcast.Speed) + .000001) / 2) * 2;
            var setup = live.Fixture.Setup!;
            var count = setup.Instructions?.Count ?? 0;
            if (count != instructionCount) { engine?.Dispose(); engine=null; checkpoint=null; instructionCount=count; }
            if (checkpoint != null && checkpoint.Second > target) { engine?.Dispose(); engine = null; checkpoint = null; }
            if (engine == null)
            {
                engine = (setup.EngineVersion == PositionalV15.Version ? PositionalV15.Stream(setup,live.Fixture.Seed) : setup.EngineVersion == PositionalV14.Version ? PositionalV14.Stream(setup,live.Fixture.Seed) : setup.EngineVersion == PositionalV13.Version ? PositionalV13.Stream(setup,live.Fixture.Seed) : setup.EngineVersion == PositionalV12.Version ? PositionalV12.Stream(setup,live.Fixture.Seed) : setup.EngineVersion == PositionalV11.Version ? PositionalV11.Stream(setup,live.Fixture.Seed) : setup.EngineVersion == PositionalV10.Version ? PositionalV10.Stream(setup,live.Fixture.Seed) : setup.EngineVersion == PositionalV9.Version ? PositionalV9.Stream(setup,live.Fixture.Seed) : setup.EngineVersion == PositionalV8.Version ? PositionalV8.Stream(setup,live.Fixture.Seed) : setup.EngineVersion == PositionalV7.Version ? PositionalV7.Stream(setup, live.Fixture.Seed) : setup.EngineVersion == PositionalV6.Version
                    ? PositionalV6.Stream(setup, live.Fixture.Seed)
                    : PositionalV5.Stream(setup, live.Fixture.Seed)).GetEnumerator();
                engine.MoveNext(); checkpoint = engine.Current;
            }
            while (!checkpoint!.Finished && checkpoint.Second < target)
            {
                if(MatchSimulation.SupportsControls(setup.EngineVersion) && !live.HalfTimeAccepted && checkpoint.Replay.Events.Any(e=>e.Type=="halftime")) break;
                int previousEventCount = checkpoint.Replay.Events.Count;
                if (!engine.MoveNext()) break;
                checkpoint = engine.Current;
                // V15: an injury of the managed team (without automatic substitution) stops the broadcast until the coach decides.
                var hurt = checkpoint.Replay.Events.Skip(previousEventCount).FirstOrDefault(e => e.Type == "injury" && e.Team == live.ManagedTeam && !live.InjuryResolved.Contains(e.Player));
                if (hurt != null && setup.EngineVersion == PositionalV15.Version && !(live.ManagedTeam == setup.Home.Id ? setup.Home : setup.Away).AutoSubstitute)
                {
                    live.InjuryAwaiting = true; live.InjuryPlayerId = hurt.Player; live.PauseAfterSecond = null; live.StoppagePaused = false;
                    live.WasPlaying = false; live.Viewers.Clear();
                    live.WatchedSeconds = LiveBroadcast.EntranceSeconds + checkpoint.Second / (double)LiveBroadcast.Speed;
                    break;
                }
                if (live.PauseAfterSecond is int armedAt &&
                    checkpoint.Replay.Events.Skip(previousEventCount).Any(e => e.Second > armedAt && Stoppages.Contains(e.Type)))
                {
                    live.PauseAfterSecond = null;
                    live.StoppagePaused = true;
                    live.WasPlaying = false;
                    live.Viewers.Clear();
                    live.WatchedSeconds = LiveBroadcast.EntranceSeconds + checkpoint.Second / (double)LiveBroadcast.Speed;
                    break;
                }
            }
            if(MatchSimulation.SupportsControls(setup.EngineVersion) && !live.HalfTimeAccepted && checkpoint!.Replay.Events.Any(e=>e.Type=="halftime"))
            {
                live.HalfTimeAwaiting=true; live.WasPlaying=false; live.PauseAfterSecond=null; live.StoppagePaused=false;
                live.WatchedSeconds=LiveBroadcast.EntranceSeconds+checkpoint.Second/(double)LiveBroadcast.Speed;
            }
            live.SimulatedSecond = checkpoint!.Second;
            live.EngineFinished = checkpoint.Finished;
            live.Duration = checkpoint.Replay.Duration;
            return checkpoint.Replay with { Events = [.. checkpoint.Replay.Events], Frames = [.. checkpoint.Replay.Frames] };
        }
    }
    public void Dispose() { lock (gate) engine?.Dispose(); }
}

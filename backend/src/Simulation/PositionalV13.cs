namespace Brasileirao;

// V12 stays frozen. V13 keeps V12 and adds realistic throw-ins: the receiver is within
// throwing range and the flight time grows with distance, so the release speed stays
// around 8-16 m/s instead of the fixed one-second flight that produced 30-50 m/s throws.
public static class PositionalV13
{
    public const string Version = "positional-v13";
    public static MatchReplay Run(MatchSetup setup, int seed, bool captureFrames) =>
        Stream(setup, seed, captureFrames).Last().Replay;

    public static IEnumerable<PositionalV5.Checkpoint> Stream(MatchSetup setup, int seed, bool captureFrames = true)
    {
        if (setup.EngineVersion != Version) throw new GameRuleException("Versão do motor inválida.");
        foreach (var step in PositionalV8.Stream(setup with { EngineVersion = PositionalV8.Version }, seed, captureFrames, new FootballBrain(seed), true, true, true, true))
            yield return step with { Replay = step.Replay with { EngineVersion = Version, Setup = setup } };
    }
}

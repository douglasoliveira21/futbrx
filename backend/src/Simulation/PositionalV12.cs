namespace Brasileirao;

// V11 stays frozen. V12 keeps the same core and enables continuous player contacts:
// a deflected physical ball uses the rest of its tick instead of returning to the
// previous position, and deflected passes are no longer reported as shots.
public static class PositionalV12
{
    public const string Version = "positional-v12";
    public static MatchReplay Run(MatchSetup setup, int seed, bool captureFrames) =>
        Stream(setup, seed, captureFrames).Last().Replay;

    public static IEnumerable<PositionalV5.Checkpoint> Stream(MatchSetup setup, int seed, bool captureFrames = true)
    {
        if (setup.EngineVersion != Version) throw new GameRuleException("Versão do motor inválida.");
        foreach (var step in PositionalV8.Stream(setup with { EngineVersion = PositionalV8.Version }, seed, captureFrames, new FootballBrain(seed), true, true, true))
            yield return step with { Replay = step.Replay with { EngineVersion = Version, Setup = setup } };
    }
}

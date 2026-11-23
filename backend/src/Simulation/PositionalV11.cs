namespace Brasileirao;

public static class PositionalV11
{
    public const string Version = "positional-v11";
    public static MatchReplay Run(MatchSetup setup, int seed, bool captureFrames) =>
        Stream(setup, seed, captureFrames).Last().Replay;

    public static IEnumerable<PositionalV5.Checkpoint> Stream(MatchSetup setup, int seed, bool captureFrames = true)
    {
        if (setup.EngineVersion != Version) throw new GameRuleException("Versão do motor inválida.");
        foreach (var step in PositionalV8.Stream(setup with { EngineVersion = PositionalV8.Version }, seed, captureFrames, new FootballBrain(seed), true, true))
            yield return step with { Replay = step.Replay with { EngineVersion = Version, Setup = setup } };
    }
}

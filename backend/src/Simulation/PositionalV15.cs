namespace Brasileirao;

// V14 stays frozen. V15 keeps V14 and makes injuries realistic: contact injuries after fouls and sliding tackles besides the
// fatigue ones, each with a type and a severity (contusion to fracture), the hurt player lies on the ground, and a team
// without automatic substitution must decide what to do with him (substitute, or play with ten).
public static class PositionalV15
{
    public const string Version = "positional-v15";
    public static MatchReplay Run(MatchSetup setup, int seed, bool captureFrames) =>
        Stream(setup, seed, captureFrames).Last().Replay;

    public static IEnumerable<PositionalV5.Checkpoint> Stream(MatchSetup setup, int seed, bool captureFrames = true)
    {
        if (setup.EngineVersion != Version) throw new GameRuleException("Versão do motor inválida.");
        foreach (var step in PositionalV8.Stream(setup with { EngineVersion = PositionalV8.Version }, seed, captureFrames, new FootballBrain(seed), true, true, true, true, true, true))
            yield return step with { Replay = step.Replay with { EngineVersion = Version, Setup = setup } };
    }
}

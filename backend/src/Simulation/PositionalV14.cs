namespace Brasileirao;

// V13 stays frozen. V14 keeps V13 and prepares throw-ins: when one is awarded, the two
// closest outfield team-mates run to short support positions (8-15 m from the taker) and
// the taker waits long enough for them to arrive, so the throw has a realistic receiver.
public static class PositionalV14
{
    public const string Version = "positional-v14";
    public static MatchReplay Run(MatchSetup setup, int seed, bool captureFrames) =>
        Stream(setup, seed, captureFrames).Last().Replay;

    public static IEnumerable<PositionalV5.Checkpoint> Stream(MatchSetup setup, int seed, bool captureFrames = true)
    {
        if (setup.EngineVersion != Version) throw new GameRuleException("Versão do motor inválida.");
        foreach (var step in PositionalV8.Stream(setup with { EngineVersion = PositionalV8.Version }, seed, captureFrames, new FootballBrain(seed), true, true, true, true, true))
            yield return step with { Replay = step.Replay with { EngineVersion = Version, Setup = setup } };
    }
}

namespace Brasileirao;

// Versioned policy adapter, not a second simulator. All physics, rules, clock,
// animations/events, halftime and shootouts use the existing incremental core.
public static class PositionalV10
{
    public const string Version="positional-v10";
    public static MatchReplay Run(MatchSetup setup,int seed,bool captureFrames)=>Stream(setup,seed,captureFrames).Last().Replay;
    public static IEnumerable<PositionalV5.Checkpoint> Stream(MatchSetup setup,int seed,bool captureFrames=true)
    {
        if(setup.EngineVersion!=Version)throw new GameRuleException("Versão da inteligência inválida.");
        foreach(var step in PositionalV8.Stream(setup with{EngineVersion=PositionalV8.Version},seed,captureFrames,new FootballBrain(seed),true))yield return step with{Replay=step.Replay with{EngineVersion=Version,Setup=setup}};
    }
}

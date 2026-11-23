namespace Brasileirao;

// Dispatch por versão: a implementação V1 permanece congelada para saves antigos.
public static class MatchSimulation
{
    public const string Version = PositionalV14.Version;
    public static bool SupportsControls(string? version)=>version is PositionalV7.Version or PositionalV8.Version or PositionalV9.Version or PositionalV10.Version or PositionalV11.Version or PositionalV12.Version or PositionalV13.Version or PositionalV14.Version;
    public const int Duration = 90 * 60;
    public static MatchReplay Run(MatchSetup setup, int seed, bool captureFrames) => setup.EngineVersion switch
    {
        PositionalV14.Version => PositionalV14.Run(setup,seed,captureFrames),
        PositionalV13.Version => PositionalV13.Run(setup,seed,captureFrames),
        PositionalV12.Version => PositionalV12.Run(setup,seed,captureFrames),
        PositionalV11.Version => PositionalV11.Run(setup,seed,captureFrames),
        PositionalV10.Version => PositionalV10.Run(setup,seed,captureFrames),
        PositionalV9.Version => PositionalV9.Run(setup,seed,captureFrames),
        PositionalV8.Version => PositionalV8.Run(setup,seed,captureFrames),
        PositionalV7.Version => PositionalV7.Run(setup, seed, captureFrames),
        PositionalV6.Version => PositionalV6.Run(setup, seed, captureFrames),
        PositionalV5.Version => PositionalV5.Run(setup, seed, captureFrames),
        PositionalV4.Version => PositionalV4.Run(setup, seed, captureFrames),
        LegacyPositionalV1.Version => LegacyPositionalV1.Run(setup, seed, captureFrames),
        PositionalV3.Version => PositionalV3.Run(setup, seed, captureFrames),
        PositionalV2.Version => PositionalV2.Run(setup, seed, captureFrames),
        _ => throw new GameRuleException("Versão do motor não suportada para esta reprodução.")
    };
}

namespace Brasileirao;
public enum OpponentBallPlay { Deflection, DeliberatePlay, DeliberateSave, GoalFrame }

// Captured when a team-mate plays the ball; participation is evaluated later.
public readonly record struct OffsideSnapshot(bool PositionedOffside, bool Exempt)
{
    public bool IsOffence(bool participates) => PositionedOffside && !Exempt && participates;
    public OffsideSnapshot AfterOpponentContact(OpponentBallPlay play)=>play==OpponentBallPlay.DeliberatePlay?new(false,false):this;
    public bool Interferes(bool challenges,bool blocksVision,bool affectsOpponent)=>IsOffence(challenges||blocksVision||affectsOpponent);

    public static OffsideSnapshot Capture(double receiverX, double receiverYaw, double ballX,
        double direction, IEnumerable<(double X, double Yaw)> defenders, bool directRestart)
    {
        bool position = receiverX * direction > 0 &&
            FootballDecisions.Offside(receiverX, receiverYaw, ballX, direction, defenders);
        return new(position, directRestart);
    }
}

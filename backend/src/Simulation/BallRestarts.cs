namespace Brasileirao;



public enum BallRestart { GoalKick, Corner, ThrowIn }

public static class BallRestarts
{
    public static bool IsOut(double x,double z) => Math.Abs(x)>52.5+BallPhysics.Radius || Math.Abs(z)>34+BallPhysics.Radius;
    public static (BallRestart Type, int Team) Resolve(double x, double z, BallContact last, int attackingDirection)
    {
        if (Math.Abs(z) > 34 + BallPhysics.Radius) return (BallRestart.ThrowIn, 1-last.Team);
        int attackingTeam = Math.Sign(x) == attackingDirection ? 0 : 1;
        return last.Team == attackingTeam ? (BallRestart.GoalKick, 1-attackingTeam) : (BallRestart.Corner, attackingTeam);
    }
}

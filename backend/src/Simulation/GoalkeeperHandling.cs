namespace Brasileirao;

// Called only after a physical contact, never when the shooter chooses a shot.
public static class GoalkeeperHandling
{
    public static bool CanHold(double speed,double height,double lateralOffset,double skill) =>
        skill>=55 && height>=BallPhysics.Radius-1e-6 && height<=2.05 &&
        lateralOffset<=.35+skill*.003 && speed<=12+skill*.1;
}

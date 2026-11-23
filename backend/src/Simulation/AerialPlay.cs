namespace Brasileirao;

public static class AerialPlay
{
    public static double Jump(double elapsed,double strength,double agility)
        => elapsed<0 || elapsed>.8 ? 0 : Math.Sin(Math.PI*elapsed/.8)*(.25+Math.Clamp((strength+agility)/200,0,1)*.5);
    public static double HeadHeight(double elapsed,double strength,double agility)=>1.72+Jump(elapsed,strength,agility);
    public static Position3 Predict(BallPhysics ball,double seconds)=>new(ball.X+ball.VX*seconds,
        Math.Max(BallPhysics.Radius,ball.Y+ball.VY*seconds-4.905*seconds*seconds),ball.Z+ball.VZ*seconds);
    public static bool Reachable(double ballHeight,double headHeight)=>ballHeight>=headHeight-.23 && ballHeight<=headHeight+BallPhysics.Radius;
}

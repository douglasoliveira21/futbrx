namespace Brasileirao;

// An impulse describes intent only; it contains no receiver or completion result.
public static class BallDelivery
{
    public static BallPhysics Launch(Position3 origin, Position3 target, double seconds, double spin=0)
    {
        if(!double.IsFinite(seconds) || seconds<=0) throw new ArgumentOutOfRangeException(nameof(seconds));
        return new(origin.X,origin.Y,origin.Z,(target.X-origin.X)/seconds,
            (target.Y-origin.Y+4.905*seconds*seconds)/seconds,(target.Z-origin.Z)/seconds,spin);
    }

    public static BallPhysics Ground(Position3 origin, Position3 target, double speed, double lateralError=0, double spin=0)
    {
        if(!double.IsFinite(speed) || speed<=0) throw new ArgumentOutOfRangeException(nameof(speed));
        double dx=target.X-origin.X,dz=target.Z-origin.Z,distance=Math.Sqrt(dx*dx+dz*dz);
        if(distance<1e-6) throw new ArgumentException("A direção do passe exige distância positiva.");
        double nx=dx/distance,nz=dz/distance;
        return new(origin.X,BallPhysics.Radius,origin.Z,speed*(nx-nz*lateralError),0,speed*(nz+nx*lateralError),spin);
    }
}

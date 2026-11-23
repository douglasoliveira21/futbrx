namespace Brasileirao;

// Coordinates: X along the pitch, Y above the ground, Z across the pitch.
public sealed class BallPhysics
{
    public const double Radius = .11;
    public double X, Y, Z, VX, VY, VZ, Spin;
    public bool FrameHit { get; private set; }

    public BallPhysics(double x, double y, double z, double vx, double vy, double vz, double spin = 0)
        => (X, Y, Z, VX, VY, VZ, Spin) = (x, y, z, vx, vy, vz, spin);

    // Stop at the first boundary substep so a fast delivery cannot travel
    // several metres out of play before the engine awards the restart.
    public void AdvanceInPitch(double seconds)
    {
        int count=Math.Max(1,(int)Math.Ceiling(seconds*120));
        for(int i=0;i<count;i++)
        {
            Step(seconds/count);
            if(Math.Abs(X)>52.5+Radius || Math.Abs(Z)>34+Radius) break;
        }
    }

    public static double? PlayerContactFraction(Position3 start, Position3 end, double x, double z, double height, double radius=.42)
    {
        double dx=end.X-start.X,dz=end.Z-start.Z,length=dx*dx+dz*dz;
        double ox=start.X-x,oz=start.Z-z,r=radius+Radius;
        double b=2*(ox*dx+oz*dz),c=ox*ox+oz*oz-r*r;
        if(c<=1e-8 && b>=0) return null; // tolerate roundoff on a separating surface contact
        double discriminant=b*b-4*length*c;
        if(discriminant<0 || length<1e-12) return null;
        double entry=(-b-Math.Sqrt(discriminant))/(2*length);
        if(entry<0 && c>1e-10) return null;
        double t=Math.Max(0,entry);
        if(t>1) return null;
        double y=start.Y+(end.Y-start.Y)*t;
        return y>=-Radius && y<=height+Radius ? t : null;
    }

    public void Step(double seconds)
    {
        const int stepsPerSecond = 120;
        int count = Math.Max(1, (int)Math.Ceiling(seconds * stepsPerSecond));
        double dt = seconds / count;
        for (int i = 0; i < count; i++)
        {
            double oldX = X, oldY = Y, oldZ = Z;
            double speed = Math.Sqrt(VX * VX + VY * VY + VZ * VZ);
            double drag = Math.Max(0, 1 - .002 * speed * dt);
            VX = VX * drag - Spin * VZ * .0006 * dt;
            VZ = VZ * drag + Spin * VX * .0006 * dt;
            VY = VY * drag - 9.81 * dt;
            X += VX * dt; Y += VY * dt; Z += VZ * dt;
            Spin *= Math.Exp(-.25 * dt);
            if (Y < Radius)
            {
                Y = Radius;
                if (VY < 0) VY = -VY * .48;
                // Friction depends on elapsed physical time, not on the number
                // of calls made by a replay or a caller with smaller substeps.
                double rolling=dt==1.0/120 ? .985 : Math.Exp(Math.Log(.985)*120*dt);
                VX *= rolling; VZ *= rolling;
                if(VX*VX+VZ*VZ<.0025 && Math.Abs(VY)<.35){VX=VZ=VY=0;}
            }
            foreach (int direction in new[] { -1, 1 })
            {
                if (Y <= 2.44 + Radius) foreach (double postZ in new[] { -3.66, 3.66 })
                    CollideCylinder(ref X, ref Z, ref VX, ref VZ, direction * 52.5, postZ, oldX, oldZ);
                // Crossbar spans the full width of the goal.
                if (Math.Abs(Z) <= 3.66 + Radius)
                    CollideCylinder(ref X, ref Y, ref VX, ref VY, direction * 52.5, 2.44, oldX, oldY);
            }
        }
    }

    void CollideCylinder(ref double a, ref double b, ref double va, ref double vb, double ca, double cb, double oldA, double oldB)
    {
        const double frameRadius = .06;
        double da = a - ca, db = b - cb, limit = Radius + frameRadius;
        double distance = Math.Sqrt(da * da + db * db);
        if (distance >= limit)
        {
            // Sweep the substep, including contacts whose two endpoints are
            // both outside the post. Endpoint overlap alone permits tunnelling.
            double deltaA=a-oldA,deltaB=b-oldB,oa=oldA-ca,ob=oldB-cb;
            double qa=deltaA*deltaA+deltaB*deltaB,qb=2*(oa*deltaA+ob*deltaB),qc=oa*oa+ob*ob-limit*limit;
            double discriminant=qb*qb-4*qa*qc;
            if(qa<1e-12 || discriminant<0) return;
            double contact=(-qb-Math.Sqrt(discriminant))/(2*qa);
            if(contact<0 || contact>1) return;
            a=oldA+deltaA*contact;b=oldB+deltaB*contact;
            da=a-ca;db=b-cb;distance=Math.Sqrt(da*da+db*db);
        }
        if (distance < 1e-9) { da = oldA - ca; db = oldB - cb; distance = Math.Max(1e-9, Math.Sqrt(da * da + db * db)); }
        double na = da / distance, nb = db / distance;
        double toward = va * na + vb * nb;
        if (toward >= 0) return;
        va -= 1.7 * toward * na; vb -= 1.7 * toward * nb;
        a = ca + na * (limit + .001); b = cb + nb * (limit + .001);
        FrameHit = true;
    }

    public bool CrossedGoalLine(double previousX, int direction) =>
        previousX * direction - Radius < 52.5 && X * direction - Radius >= 52.5 &&
        Math.Abs(Z) + Radius < 3.66 && Y + Radius < 2.44;
}


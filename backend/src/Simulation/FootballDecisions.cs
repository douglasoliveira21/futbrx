namespace Brasileirao;

public static class FootballDecisions
{
    public enum Sanction { None, Yellow, Red }
    public static Sanction Foul(bool reckless, bool excessive, bool deniesGoal, bool inArea, bool attemptsBall, bool stopsAttack = false)
        => excessive ? Sanction.Red : deniesGoal ? inArea && attemptsBall ? Sanction.Yellow : Sanction.Red : reckless || stopsAttack && !(inArea && attemptsBall) ? Sanction.Yellow : Sanction.None;
    public static double LegalBodyPoint(double x, double yaw, double direction) => x * direction + Math.Max(.12, Math.Max(Math.Sin(yaw) * direction * .32, -Math.Sin(yaw) * direction * .25));
    public static bool Offside(double x, double yaw, double ballX, double direction, IEnumerable<(double X, double Yaw)> opponents)
    {
        var line = opponents.Select(p => LegalBodyPoint(p.X, p.Yaw, direction)).OrderDescending().ToArray();
        var body = LegalBodyPoint(x, yaw, direction);
        return line.Length >= 2 && body > 0 && body > ballX * direction + .24 + .001 && body > line[1] + .001;
    }
    public static double PassingLaneDistance(double x, double z, double ax, double az, double bx, double bz)
    {
        var dx = bx - ax; var dz = bz - az; var square = dx * dx + dz * dz;
        var t = square == 0 ? 0 : Math.Clamp(((x - ax) * dx + (z - az) * dz) / square, 0, 1);
        return Math.Sqrt(Math.Pow(x - ax - dx * t, 2) + Math.Pow(z - az - dz * t, 2));
    }
}

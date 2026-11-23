namespace Brasileirao;

public static class MatchRules
{
    // A posição é avaliada no toque do passe, no campo adversário, incluindo o goleiro entre os defensores.
    public static bool IsOffside(double receiverX, double ballX, double direction, IEnumerable<double> opponents)
    {
        var defenders = opponents.Select(x => x * direction).OrderByDescending(x => x).ToArray();
        return defenders.Length >= 2 && receiverX * direction > 0 && receiverX * direction > ballX * direction + .01 && receiverX * direction > defenders[1] + .01;
    }
    public static bool InPenaltyArea(double x, double z, double direction) => x * direction >= 36 && x * direction <= 52.5 && Math.Abs(z) <= 20.16;
    public static (int HalfEnd, int Duration) EnsurePenaltyTime(int second, int halfEnd, int duration)
    {
        int requiredEnd = second + 22;
        if (second >= 2700 && second < halfEnd && requiredEnd > halfEnd)
            return (requiredEnd, duration + requiredEnd - halfEnd);
        if (second >= halfEnd + 2700) return (halfEnd, Math.Max(duration, requiredEnd));
        return (halfEnd, duration);
    }
    public static int AddedTime(IEnumerable<MatchEvent> events, int from, int until)
    {
        int seconds = events.Where(e => e.Second >= from && e.Second < until).Sum(e => e.Type switch { "substitution" => 30, "injury" => 60, "goal" => 45, "foul" => 15, "offside" => 8, _ => 0 });
        return Math.Max(60, (seconds + 59) / 60 * 60);
    }
    public static (double Ax, double Az, double Bx, double Bz) Separate(double ax, double az, double bx, double bz, double radius)
    {
        double dx = bx - ax, dz = bz - az, squared = dx * dx + dz * dz, minimum = radius * 2;
        if (squared >= minimum * minimum) return (ax, az, bx, bz);
        double distance = Math.Sqrt(squared);
        if (distance < .00001) { dx = 1; dz = 0; distance = .00001; }
        double length = Math.Sqrt(dx * dx + dz * dz), move = (minimum - distance) / 2;
        return (ax - dx / length * move, az - dz / length * move, bx + dx / length * move, bz + dz / length * move);
    }
}

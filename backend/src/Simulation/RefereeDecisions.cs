namespace Brasileirao;

// Explicit incident facts, not random overturns. This subset follows IFAB 2025/26.
public static class RefereeDecisions
{
    public static bool Advantage(bool safeToContinue, bool retainsPossession, bool promisingAttack)
        => safeToContinue && retainsPossession && promisingAttack;
    public static bool Handball(bool deliberate, bool unnaturallyBigger, bool scorerImmediately, bool keeperInsideOwnArea)
        => !keeperInsideOwnArea && (deliberate || unnaturallyBigger || scorerImmediately);
    public static bool ObstructsView(double x, double z, double keeperX, double keeperZ, double ballX, double ballZ)
    {
        var dx = ballX - keeperX; var dz = ballZ - keeperZ; var square = dx * dx + dz * dz;
        if (square < .01) return false;
        var t = ((x - keeperX) * dx + (z - keeperZ) * dz) / square;
        return t > .02 && t < .98 && FootballDecisions.PassingLaneDistance(x, z, keeperX, keeperZ, ballX, ballZ) < .4;
    }
    public static bool Reviewable(string incident) => incident is "goal" or "penalty" or "direct_red" or "mistaken_identity";
    public static (double X, double Z) ControlPoint(double x, double z, double yaw, double physicalSecond, bool keeper)
    {
        if (keeper) return (x, z);
        var reach = .33 + .09 * Math.Sin(physicalSecond * Math.PI * 2 / .9);
        return (x + Math.Sin(yaw) * reach + Math.Cos(yaw) * .085,
                z + Math.Cos(yaw) * reach - Math.Sin(yaw) * .085);
    }
}

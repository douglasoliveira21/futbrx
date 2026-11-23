namespace Brasileirao;

public sealed record SubstitutionPlan(int Minute, int OutPlayerId, int InPlayerId);
public sealed class SeasonPlayerStats
{
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public string ClubId { get; set; } = "";
    public int Appearances { get; set; }
    public int Starts { get; set; }
    public int Minutes { get; set; }
    public int Goals { get; set; }
    public int Shots { get; set; }
    public int Passes { get; set; }
    public int Interceptions { get; set; }
    public int Fouls { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
}
public sealed record MatchPlayerStats(int PlayerId, string PlayerName, string ClubId, int Minutes, bool Started, int Goals, int Shots, int Passes, int Interceptions, int Fouls, int YellowCards, int RedCards);
public sealed record AvailabilityIssue(int PlayerId, string Name, string Reason, int InjuryDays, int SuspensionMatches);

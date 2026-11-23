namespace Brasileirao;

public sealed class ScoutingMission
{
    public int Season { get; set; }
    public int StartedRound { get; set; }
    public int CompletionRound { get; set; }
    public int ExpiresRound { get; set; }
    public string Region { get; set; } = "";
    public string Position { get; set; } = "Todas";
    public string Status { get; set; } = "searching";
    public int Seed { get; set; }
}
public sealed record YouthProspect(Player Player, string Region, long SigningFee);

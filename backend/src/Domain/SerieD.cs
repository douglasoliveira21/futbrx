namespace Brasileirao;

// Final table of one of the 16 groups of Série D (only the standings are kept for the groups the user does not play).
public sealed class SerieDGroupResult
{
    public int Group { get; set; }
    public List<SeasonStanding> Table { get; set; } = [];
}

public sealed class SerieDState
{
    // 1 to 16: the group the managed club plays in.
    public int Group { get; set; }
    // The 90 clubs of the other groups: name, colour and strength are all that exists of them until they are met.
    public List<Club> Others { get; set; } = [];
    // Group of every club of the series, by club id.
    public Dictionary<string, int> Groups { get; set; } = [];
    // Filled when the group stage ends: the 16 final tables and the 64 qualified clubs in seed order.
    public List<SerieDGroupResult> Results { get; set; } = [];
    public List<string> Qualified { get; set; } = [];
    // Six clubs go up: the finalists, the semifinal losers and the two playoff winners.
    public List<string> Promoted { get; set; } = [];
}

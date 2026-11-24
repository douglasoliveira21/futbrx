namespace Brasileirao;

// Only the public broadcast projection is sent to clients; PreparedState stays on the server.
public sealed class LiveMatch
{
    public Dictionary<string,LiveViewer> Viewers { get; set; } = [];
    public string CompetitionMatchId {get;set;}="";
    public string CompetitionName {get;set;}="";
    public long StartingBalance { get; set; }
    public bool Incremental { get; set; }
    public int SimulatedSecond { get; set; }
    public bool EngineFinished { get; set; }
    public int PlaybackSpeed { get; set; } = 12;
    public int? PauseAfterSecond { get; set; }
    public bool StoppagePaused { get; set; }
    public bool HalfTimeAwaiting { get; set; }
    public bool HalfTimeAccepted { get; set; }
    // V15: an injured player of the managed team (without automatic substitution) waits for the coach's decision; the broadcast
    // stays paused until the player is substituted or retired. InjuryResolved lists the players already decided.
    public bool InjuryAwaiting { get; set; }
    public int InjuryPlayerId { get; set; }
    public List<int> InjuryResolved { get; set; } = [];
    public string ManagedTeam { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    // V5: only pre-match preparation, without results. Legacy V4 sessions stored a completed round here.
    public string PreparedState { get; set; } = "";
    public Fixture Fixture { get; set; } = new();
    public int Season { get; set; }
    public double WatchedSeconds { get; set; }
    public DateTimeOffset LastHeartbeat { get; set; }
    public bool WasPlaying { get; set; }
    public int Duration { get; set; }
}

public sealed record LiveViewer(DateTimeOffset LastSeen,bool Playing);

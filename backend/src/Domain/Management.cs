namespace Brasileirao;

public sealed class PlayerContract
{
    public int PlayerId { get; set; }
    public string ClubId { get; set; } = "";
    public int StartSeason { get; set; }
    public int StartRound { get; set; }
    public int EndSeason { get; set; }
    public int Wage { get; set; }
    public long SigningBonus { get; set; }
    public int Revision { get; set; } = 1;
    public bool Active { get; set; } = true;
    public long ReleaseClause { get; set; }
}
public sealed class TransferOffer
{
    public long AgentCommission {get;set;}
    public string AgentName {get;set;}="";
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public string Kind { get; set; } = "inbound";
    public string FromClub { get; set; } = "";
    public string ToClub { get; set; } = "";
    public long Fee { get; set; }
    public int Wage { get; set; }
    public int Years { get; set; }
    public long SigningBonus { get; set; }
    public int Season { get; set; }
    public int CreatedRound { get; set; }
    public int ResponseRound { get; set; }
    public int ExpiresRound { get; set; }
    public int ContractRevision { get; set; }
    public string Status { get; set; } = "pending";
    public string Message { get; set; } = "";
    public long? CounterFee { get; set; }
    public int? CounterWage { get; set; }
    public int Installments { get; set; } = 1;
    public long ReleaseClause { get; set; }
}
public sealed class LoanDeal
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PlayerId { get; set; }
    public string PlayerName { get; set; } = "";
    public string OwnerClub { get; set; } = "";
    public string BorrowerClub { get; set; } = "";
    public int EndSeason { get; set; }
    public long Fee { get; set; }
    public bool Returned { get; set; }
}
public sealed class TransferPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int PlayerId { get; set; }
    public string Buyer { get; set; } = "";
    public string Seller { get; set; } = "";
    public int DueSeason { get; set; }
    public int DueRound { get; set; }
    public long Amount { get; set; }
    public bool Paid { get; set; }
}
public sealed record TransferRecord(int Season, int Round, int PlayerId, string PlayerName, string FromClub, string ToClub, long Fee, long SigningBonus, int Wage, int EndSeason, string Kind);
public sealed record TransferWindow(bool IsOpen, string Name, int? ClosingRound, int? NextOpeningRound, string Description);
public sealed record PlayerQuote(int PlayerId, string PlayerName, string FromClub, string Kind, long Fee, int Wage, long SigningBonus, int Years, int MinYears, bool Available, string Reason, long AgentCommission=0,string AgentName="");
public sealed record MarketCandidate(Player Player, string ClubId, long AskingFee, int WageDemand, int? ContractEndSeason, bool Available, string Reason);
public sealed class PastSeason
{
    public DivisionSeason? Divisions { get; set; }
    public List<Club> Clubs { get; set; } = [];
    public int PlayerStatsFromRound { get; set; } = 1;
    public List<SeasonPlayerStats> PlayerStats { get; set; } = [];
    public int Season { get; set; }
    public string Champion { get; set; } = "";
    public List<Fixture> Fixtures { get; set; } = [];
    public List<SeasonStanding> Table { get; set; } = [];
}
public sealed record SeasonStanding(string ClubId, int Points, int Played, int Wins, int Draws, int Losses, int Goals, int Against, int Difference);

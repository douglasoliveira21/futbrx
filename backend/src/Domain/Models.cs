namespace Brasileirao;

public class Account { public Guid Id { get; set; } = Guid.NewGuid(); public string Email { get; set; } = ""; public string PasswordHash { get; set; } = ""; public string Name { get; set; } = ""; }
public class Session { public Guid Id { get; set; } = Guid.NewGuid(); public Guid UserId { get; set; } public string Hash { get; set; } = ""; public DateTime Expires { get; set; } public DateTime Created { get; set; } = DateTime.UtcNow; public string Device { get; set; } = ""; }
public class Career { public Guid Id { get; set; } = Guid.NewGuid(); public Guid UserId { get; set; } public string Name { get; set; } = ""; public string State { get; set; } = ""; public int Revision { get; set; } public DateTime Updated { get; set; } = DateTime.UtcNow; }
public class Audit { public long Id { get; set; } public Guid UserId { get; set; } public string Action { get; set; } = ""; public DateTime At { get; set; } = DateTime.UtcNow; }
public record Club(string Id, string Name, string Short, string Color, int Strength);
public class Player { public string PreferredFoot {get;set;}="Não informado"; public int Id { get; set; } public string Name { get; set; } = ""; public string Position { get; set; } = ""; public int Age { get; set; } public int Rating { get; set; } public int Fitness { get; set; } = 100; public int Value { get; set; } public int Wage { get; set; } public bool Starter { get; set; } public int InjuryDays { get; set; } public bool TransferListed { get; set; } public int YellowCards { get; set; } public int SuspensionMatches { get; set; } public string InjuryName { get; set; } = ""; }
public class Fixture { public int Leg {get;set;} public bool Rescheduled {get;set;} public string CompetitionMatchId {get;set;}="";public string CompetitionName {get;set;}=""; public int? HomePenalties {get;set;} public int? AwayPenalties {get;set;}  public DateOnly? Date { get; set; } public DateTimeOffset? Kickoff { get; set; } public int Round { get; set; } public string Home { get; set; } = ""; public string Away { get; set; } = ""; public int? HomeGoals { get; set; } public int? AwayGoals { get; set; } public List<MatchEvent> Events { get; set; } = []; public int Seed { get; set; } public MatchSetup? Setup { get; set; } public List<MatchPlayerStats> PlayerStats { get; set; } = []; }
public record MatchEvent(int Minute, string Type, string Team, int Player, string Text, int Second = -1, int? RelatedPlayer = null, int InjuryDays = 0);
public record Ledger(int Round, string Description, long Amount, int Season = 0);
public class GameState
{
    public Dictionary<string,List<SavedTacticalPlan>> SavedTactics {get;set;}=[];
    public TacticalPlan TacticalPlan {get;set;}=new();
    public ClubLifeState? Life { get; set; }
    public LiveMatch? LiveMatch { get; set; }
    public int CalendarVersion { get; set; }
    public string CalendarSource { get; set; } = "Calendário demonstrativo por rodadas";
    public string LotterySeed { get; set; } = "legacy-draw";
    // Length of the season in weekly rounds and the round where the main league starts.
    public int SeasonRounds { get; set; } = 38;
    public int LeagueStart { get; set; } = 1;
    public DivisionSeason? Divisions { get; set; }
    public List<LoanDeal> Loans { get; set; } = [];
    public List<TransferPayment> TransferPayments { get; set; } = [];
    public int YouthVersion { get; set; }
    public List<Player> Academy { get; set; } = [];
    public ScoutingMission? Scouting { get; set; }
    public List<YouthProspect> YouthProspects { get; set; } = [];
    public List<string> YouthHistory { get; set; } = [];
    public int ScoutingSequence { get; set; }
    public int NextYouthPlayerId { get; set; }
    public int TrainingVersion { get; set; }
    public TrainingPlan WeeklyTraining { get; set; } = new();
    public Dictionary<int, PlayerDevelopment> Development { get; set; } = [];
    public List<TrainingReport> TrainingReports { get; set; } = [];
    public int LastTrainingSeason { get; set; }
    public int LastTrainingRound { get; set; }
    public int SportsVersion { get; set; }
    public List<int> BenchIds { get; set; } = [];
    public List<SubstitutionPlan> Substitutions { get; set; } = [];
    public bool AutoSubstitute { get; set; } = true;
    public bool AutoReplaceUnavailable { get; set; } = true;
    public int PlayerStatsFromRound { get; set; } = 1;
    public Dictionary<int, SeasonPlayerStats> PlayerStats { get; set; } = [];
    public int ManagementVersion { get; set; }
    public Dictionary<int, PlayerContract> Contracts { get; set; } = [];
    public Dictionary<string, long> ClubBalances { get; set; } = [];
    public List<TransferOffer> Offers { get; set; } = [];
    public List<TransferRecord> Transfers { get; set; } = [];
    public List<PastSeason> PastSeasons { get; set; } = [];
    public Dictionary<string, List<Player>> OpponentSquads { get; set; } = [];
    public string DataVersion { get; set; } = "fictional-2026-v1"; public int Season { get; set; } = 2026; public int Round { get; set; } = 1; public string ClubId { get; set; } = "pal"; public string Formation { get; set; } = "4-3-3"; public string Mentality { get; set; } = "Equilibrada"; public string Training { get; set; } = "Técnico"; public long Balance { get; set; } = 45000000; public List<Club> Clubs { get; set; } = []; public List<Player> Squad { get; set; } = []; public List<Player> Market { get; set; } = []; public List<Fixture> Fixtures { get; set; } = []; public List<Ledger> Ledger { get; set; } = []; public List<string> News { get; set; } = ["Bem-vindo ao clube. A diretoria espera uma campanha entre os seis primeiros."]; public List<string> History { get; set; } = [];
}


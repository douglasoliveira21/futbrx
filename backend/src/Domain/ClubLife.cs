namespace Brasileirao;

public sealed class ClubLifeState
{
    public CoachWorld? Coaches {get;set;}
    public int Version {get;set;}=1;
    public int CalendarVersion {get;set;}
    public int Season {get;set;}
    public BoardState Board {get;set;}=new();
    public SupporterState Fans {get;set;}=new();
    public Dictionary<int,PlayerMind> Minds {get;set;}=[];
    public Dictionary<int,int> HiddenPotential {get;set;}=[];
    public Dictionary<int,MedicalPlan> Medical {get;set;}=[];
    public Dictionary<string,List<Player>> ClubAcademies {get;set;}=[];
    public Dictionary<string,List<StaffMember>> ClubStaff {get;set;}=[];
    public List<StaffMember> Staff {get;set;}=[];
    public List<PressStory> Press {get;set;}=[];
    public List<ScoutReport> Reports {get;set;}=[];
    public List<YouthProspect> Trials {get;set;}=[];
    public int LastTrialRound {get;set;}=-10;
    public int LastInterviewRound {get;set;}=-1;
    public Dictionary<string,ClubEconomy> Accounts {get;set;}=[];
    public int LastSettledSeason {get;set;}
    public int LastSettledRound {get;set;}
    public List<Club> ExtraClubs {get;set;}=[];
    public List<ClubCompetition> Competitions {get;set;}=[];
    public List<ArchivedCompetition> CupHistory {get;set;}=[];
    public List<string> Honours {get;set;}=[];
}
public sealed class BoardState
{
    public int Confidence {get;set;}=65;
    public int TargetPosition {get;set;}=10;
    public long WageBudget {get;set;}
    public long TransferBudget {get;set;}
    public long FundingReserve {get;set;}=10_000_000;
    public int Warnings {get;set;}
    public bool Dismissed {get;set;}
    public int LastFundingSeason {get;set;}
    public int CaptainId {get;set;}=-1;
    public string Assessment {get;set;}="A diretoria aguarda as primeiras avaliações.";
}
public sealed class PlayerMind
{
    public int Morale {get;set;}=65;
    public int Ambition {get;set;}
    public int Leadership {get;set;}
    public int Relationship {get;set;}=55;
    public string Personality {get;set;}="Equilibrado";
    public int LastTalkRound {get;set;}=-1;
    public int PromiseDueRound {get;set;}
    public int PromisedMinutes {get;set;}
    public int MinutesAtPromise {get;set;}
}
public sealed class SupporterState
{
    public int Popularity {get;set;}=60;
    public int Support {get;set;}=65;
    public int LossStreak {get;set;}
    public string RivalId {get;set;}="";
    public string Mood {get;set;}="Apoiando a equipe";
}
public sealed class ClubEconomy
{
    public int TicketPrice {get;set;}=55;
    public int Capacity {get;set;}=35000;
    public string Sponsor {get;set;}="Regional";
    public long SponsorWeekly {get;set;}=1_000_000;
    public int SponsorTarget {get;set;}=16;
    public int SponsorSignedSeason {get;set;}
    public long Debt {get;set;}
    public long Instalment {get;set;}
    public int DebtRounds {get;set;}
    public int Attendance {get;set;}
    public long LastGate {get;set;}
}
public sealed class MedicalPlan
{
    public string Treatment {get;set;}="Padrão";
    public int PreviousDays {get;set;}
    public int ClearedRound {get;set;}=-10;
    public int LastTreatmentRound {get;set;}=-1;
    public int Relapses {get;set;}
}
public sealed record StaffMember(string Id,string Name,string Role,int Quality,int Wage);
public sealed class PressStory
{
    public Guid Id {get;set;}=Guid.NewGuid();
    public int Season {get;set;}
    public int Round {get;set;}
    public string Kind {get;set;}="Notícia";
    public string Title {get;set;}="";
    public string Text {get;set;}="";
    public string Answer {get;set;}="";
}
public sealed class ScoutReport
{
    public int PlayerId {get;set;}
    public int DueRound {get;set;}
    public int Season {get;set;}
    public int Quality {get;set;}
    public bool Complete {get;set;}
}
public sealed class ClubCompetition
{
    public string Id {get;set;}="";
    public string Name {get;set;}="";
    public string Format {get;set;}="Mata-mata em jogo único; empate decidido nos pênaltis. Formato gerado e simplificado.";
    public List<int> Rounds {get;set;}=[];
    public int Stage {get;set;}
    // 1 = single match per tie; 2 = home and away legs, decided on aggregate goals and then penalties.
    public int Legs {get;set;}=1;
    // Weeks between the first and the second leg.
    public int LegGap {get;set;}=1;
    // Light: ties between clubs the user does not manage are played without squads, from club strength only.
    public bool Light {get;set;}
    // Promotion: the winner of the tie earns a place in the next division instead of a title.
    public bool Promotion {get;set;}
    // GroupStage: a mini league (every club plays the others home and away); the matches have no winner, the table decides.
    // Cota paid to the managed club when its tie of that stage is decided (index = stage), and the runner-up prize of the final.
    public List<long> StagePrizes {get;set;}=[];
    public long RunnerUpPrize {get;set;}
    public bool GroupStage {get;set;}
    // The first club of each tie hosts the first leg (no draw of the venues).
    public bool KeepOrder {get;set;}
    // A level aggregate is decided for the club that hosts the second leg (the better placed): no extra time, no penalties.
    public bool NoPenalties {get;set;}
    // GroupStage: the clubs that go on once the groups are over.
    public List<string> Advanced {get;set;}=[];
    public List<CupMatch> Matches {get;set;}=[];
    public string Champion {get;set;}="";
    public long Prize {get;set;}
}
public sealed class CupMatch
{
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public int Stage {get;set;}
    // Both legs of a tie share the Tie id; Leg is 1 or 2 (1 for single-match ties).
    public string Tie {get;set;}="";
    public int Leg {get;set;}=1;
    public Fixture Fixture {get;set;}=new();
    // Set on every leg of the tie once it is decided.
    public string Winner {get;set;}="";
}

public sealed record PotentialEstimate(int Min,int Max);

public sealed record ArchivedCompetition(int Season,ClubCompetition Competition);

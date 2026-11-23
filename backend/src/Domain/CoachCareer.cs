namespace Brasileirao;

// Stored inside the existing, revision-protected career snapshot.
public sealed class CoachWorld
{
    public int Version { get; set; } = 1;
    public DateOnly Date { get; set; }
    public int LastTickDay { get; set; }
    public CoachBalance Rules { get; set; } = new();
    public string PlayerId { get; set; } = "player";
    public List<CoachPerson> People { get; set; } = [];
    public Dictionary<string,CoachBoard> Boards { get; set; } = [];
    public List<CoachContract> Contracts { get; set; } = [];
    public List<CoachOffer> Offers { get; set; } = [];
    public List<CoachApplication> Applications { get; set; } = [];
    public List<CoachVacancy> Vacancies { get; set; } = [];
    public List<CoachEvent> Timeline { get; set; } = [];
    public HashSet<string> ProcessedMatches { get; set; } = [];
    public HashSet<string> AwardedTitles { get; set; } = [];
    public HashSet<string> PaidRounds { get; set; } = [];
    public List<CoachDismissal> Dismissals { get; set; } = [];
    public Dictionary<string,ClubCoachStorage> ClubStorage { get; set; } = [];
    public int Sequence { get; set; }
}
public sealed class CoachBalance
{
    public int[] ConfidenceBands { get; set; } = [90,75,60,45,30,15];
    public int UltimatumThreshold { get; set; } = 30;
    public int DismissalThreshold { get; set; } = 20;
    public int CriticalThreshold { get; set; } = 10;
    public int UltimatumGames { get; set; } = 4;
    public int UltimatumWins { get; set; } = 2;
    public int OfferValidityDays { get; set; } = 14;
    public int VacancySelectionDays { get; set; } = 14;
    public int ReputationTolerance { get; set; } = 18;
    public int AiRetirementAge { get; set; } = 70;
    public int CriticalDismissalChance { get; set; } = 90;
    public int BaseDismissalChance { get; set; } = 35;
    public int ApplicationChance { get; set; } = 60;
    public int InformalInterestChance { get; set; } = 55;
}
public sealed class CoachPerson
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public DateOnly BirthDate { get; set; }
    public string Nationality { get; set; } = "Brasil";
    public string ClubId { get; set; } = "";
    public double Reputation { get; set; } = 35;
    public string Formation { get; set; } = "4-3-3";
    public string Style { get; set; } = "Equilibrada";
    public int YouthAffinity { get; set; } = 50;
    public bool Retired { get; set; }
    public List<CoachResult> Results { get; set; } = [];
    public List<string> Titles { get; set; } = [];
    public List<string> Awards { get; set; } = [];
    public List<CoachReputation> ReputationHistory { get; set; } = [];
}
public sealed record CoachResult(string Key,string ClubId,DateOnly Date,int Season,int Scored,int Against,bool League,int Points,int YouthMinutes,string ContractId="");
public sealed record CoachReputation(DateOnly Date,int Season,double Value,string Reason);
public sealed class CoachContract
{
    public string Id { get; set; } = "";
    public string CoachId { get; set; } = "";
    public string ClubId { get; set; } = "";
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    public DateOnly? Closed { get; set; }
    public string Status { get; set; } = "Ativo";
    public long Wage { get; set; }
    public int SeveranceMonths { get; set; } = 3;
    public int TargetPosition { get; set; }
    public long TransferBudget { get; set; }
    public string EndReason { get; set; } = "";
    public string PreviousContract { get; set; } = "";
}
public sealed class CoachBoard
{
    public string ClubId { get; set; } = "";
    public int Patience { get; set; }
    public int Ambition { get; set; }
    public int TitleDemand { get; set; }
    public int LossTolerance { get; set; }
    public int ExperiencePreference { get; set; }
    public int YouthPreference { get; set; }
    public int AcademyValue { get; set; }
    public string PreferredStyle { get; set; } = "Equilibrada";
    public int Confidence { get; set; } = 65;
    public int PreviousPosition { get; set; }
    public int TargetPosition { get; set; }
    public int VacancyVersion { get; set; }
    public DateOnly LastDecision { get; set; }
    public List<CoachAssessment> Assessments { get; set; } = [];
    public CoachUltimatum? Ultimatum { get; set; }
}
public sealed record CoachAssessment(DateOnly Date,int Confidence,string Reason,Dictionary<string,double> Factors);
public sealed class CoachUltimatum
{
    public string Kind { get; set; } = "wins";
    public string Description { get; set; } = "";
    public int Required { get; set; }
    public int GamesLeft { get; set; }
    public int Progress { get; set; }
    public bool Resolved { get; set; }
    public bool Success { get; set; }
}
public sealed class CoachVacancy
{
    public string Id { get; set; } = "";
    public string ClubId { get; set; } = "";
    public int Version { get; set; }
    public DateOnly Opened { get; set; }
    public DateOnly Deadline { get; set; }
    public string Status { get; set; } = "Aberta";
    public string Reason { get; set; } = "";
    public List<string> Candidates { get; set; } = [];
}
public sealed class CoachApplication
{
    public string Id { get; set; } = "";
    public string VacancyId { get; set; } = "";
    public DateOnly Applied { get; set; }
    public DateOnly AnswerDate { get; set; }
    public string Status { get; set; } = "Em análise";
    public string Reason { get; set; } = "";
}
public sealed class CoachOffer
{
    public string Id { get; set; } = "";
    public string ClubId { get; set; } = "";
    public string VacancyId { get; set; } = "";
    public int VacancyVersion { get; set; }
    public string Kind { get; set; } = "Contratação";
    public string Status { get; set; } = "Interesse informal";
    public DateOnly Created { get; set; }
    public DateOnly Expires { get; set; }
    public long Wage { get; set; }
    public int Years { get; set; } = 2;
    public int TargetPosition { get; set; }
    public long TransferBudget { get; set; }
    public int SeveranceMonths { get; set; } = 3;
    public long Buyout { get; set; }
    public int NegotiationRound { get; set; }
    public string Message { get; set; } = "";
    public List<string> NegotiationHistory { get; set; } = [];
}
public sealed class CoachDismissal
{
    public string Id { get; set; } = "";
    public string CoachName { get; set; } = "";
    public string ClubId { get; set; } = "";
    public DateOnly Date { get; set; }
    public int Days { get; set; }
    public int Played { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int Position { get; set; }
    public List<string> Titles { get; set; } = [];
    public string Reason { get; set; } = "";
    public long Compensation { get; set; }
    public string Reaction { get; set; } = "";
    public bool Acknowledged { get; set; }
}
public sealed record CoachEvent(string Id,DateOnly Date,string Kind,string CoachId,string ClubId,string Title,string Text);
public sealed class ClubCoachStorage
{
    public BoardState Board { get; set; } = new();
    public SupporterState Fans { get; set; } = new();
    public List<Ledger> Ledger { get; set; } = [];
}
public sealed record CoachAction(int Revision,string Type,string Id="",string Value="",long Wage=0,int Years=0,int Target=0,long Budget=0,int SeveranceMonths=0,int Days=7);

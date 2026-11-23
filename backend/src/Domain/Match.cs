namespace Brasileirao;

public sealed record MatchPlayer(int Id, string Name, string Position, int Rating, int Fitness, string NaturalPosition = "",Dictionary<string,int>? Skills=null,string PreferredFoot="Não informado");
public sealed record MatchTeam(string Id, string Formation, string Mentality, List<MatchPlayer> Players, List<MatchPlayer>? Bench = null, List<SubstitutionPlan>? Substitutions = null, bool AutoSubstitute = true,TacticalPlan? Plan=null,bool ComputerControlled=false);
public sealed record MatchInstruction(int Second, string Team, string Type, string Formation = "", string Mentality = "", int OutPlayerId = 0, int InPlayerId = 0, bool AtHalfTime = false,TacticalPlan? Plan=null);
public sealed record MatchSetup(string EngineVersion, MatchTeam Home, MatchTeam Away, List<MatchInstruction>? Instructions = null, bool Knockout = false);
public sealed record Position3(double X, double Y, double Z);
public sealed record ReplayDiagnostics(string State, string? Restart, int? EventIndex);
public sealed record PlayerPose(int Id, int Team, double X, double Z, double Yaw, string Action, bool Active = true, string Name = "", string Position = "", int ActionSecond = -1, [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? Fitness = null);
public sealed record ReplayFrame(int Second, List<PlayerPose> Players, Position3 Ball, [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? Possession = null, [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] BallContact? LastContact = null, [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Position3? BallVelocity = null, [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] ReplayDiagnostics? Diagnostics = null);
public sealed record MatchReplay(string EngineVersion, int Duration, MatchSetup Setup, List<MatchEvent> Events, List<ReplayFrame> Frames, int HomeGoals, int AwayGoals, List<MatchPlayerStats>? PlayerStats = null, int? HomePenalties = null, int? AwayPenalties = null);

public sealed record TacticalPoint(double X,double Z);
public sealed record SavedTacticalPlan(string Id,string Name,string FormationId,string Mentality,TacticalPlan Plan);
public sealed record TacticalPlan(string Style="Equilibrado",string Pressure="Média",string Width="Normal",bool BuildShort=true,bool Overlap=false,bool ShootMore=false,bool AvoidFouls=false,bool OffsideTrap=false,string AttackSide="Ambos",Dictionary<int,string>? Roles=null,Dictionary<int,TacticalPoint>? Positions=null,string? Tempo=null,string? DefensiveTransition=null);

public sealed record BallContact(int PlayerId, int Team, string Type, int Second, double X, double Y, double Z, string Origin, int Id = 0);


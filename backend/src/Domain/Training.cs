namespace Brasileirao;

public sealed class TrainingPlan
{
    public List<string> Days { get; set; } = ["Passe", "Defesa", "Físico", "Finalização", "Recuperação", "Descanso"];
    public string Intensity { get; set; } = "Normal";
}
public sealed class PlayerDevelopment
{
    public int PlayerId { get; set; }
    public string Focus { get; set; } = "Equipe";
    public Dictionary<string, int> Attributes { get; set; } = [];
    public Dictionary<string, int> Experience { get; set; } = [];
}
public sealed record AttributeGain(string Attribute, int Before, int After);
public sealed record TrainingPlayerReport(int PlayerId, string Name, bool Injured, int FitnessBefore, int FitnessAfter, int RatingBefore, int RatingAfter, Dictionary<string, int> Experience, List<AttributeGain> Gains, string Group = "Profissional");
public sealed record TrainingReport(int Season, int Round, TrainingPlan Plan, List<TrainingPlayerReport> Players);

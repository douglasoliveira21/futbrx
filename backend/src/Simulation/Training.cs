namespace Brasileirao;

public static class Training
{
    public static readonly string[] Attributes = ["Passe", "Finalização", "Defesa", "Físico", "Goleiro"];
    public static readonly string[] Sessions = [.. Attributes, "Recuperação", "Descanso"];
    public static readonly string[] Intensities = ["Leve", "Normal", "Alta"];
    static IEnumerable<Player> Players(GameState g) => g.Squad.Concat(g.OpponentSquads.Values.SelectMany(s => s)).Concat(g.Market).Concat(g.Academy);
    public static TrainingPlan Preset(string name) => name switch
    {
        "Recuperação" => new() { Days = ["Recuperação", "Descanso", "Recuperação", "Passe", "Recuperação", "Descanso"], Intensity = "Leve" },
        "Físico" => new() { Days = ["Físico", "Físico", "Recuperação", "Físico", "Passe", "Descanso"] },
        "Técnico" => new(),
        _ => throw new GameRuleException("Foco de treino inválido.")
    };
    public static void Upgrade(GameState g)
    {
        if (g.TrainingVersion == 0) { g.WeeklyTraining = Preset(g.Training); g.TrainingVersion = 1; }
        foreach (var p in Players(g))
            if (!g.Development.ContainsKey(p.Id)) g.Development[p.Id] = new() { PlayerId = p.Id, Attributes = Attributes.ToDictionary(a => a, _ => p.Rating), Experience = Attributes.ToDictionary(a => a, _ => 0) };
        FootballSkills.Upgrade(g);
    }
    public static void SavePlan(GameState g, TrainingPlan plan)
    {
        if (plan == null || plan.Days == null || plan.Days.Count != 6 || plan.Days.Any(d => !Sessions.Contains(d)) || !Intensities.Contains(plan.Intensity)) throw new GameRuleException("Informe seis dias válidos e intensidade Leve, Normal ou Alta.");
        if (!plan.Days.Any(d => d is "Descanso" or "Recuperação")) throw new GameRuleException("Reserve pelo menos um dia para descanso ou recuperação.");
        g.WeeklyTraining = new() { Days = [.. plan.Days], Intensity = plan.Intensity }; g.Training = "Personalizado";
    }
    public static void SetPreset(GameState g, string name) { var plan = Preset(name); g.WeeklyTraining = plan; g.Training = name; }
    public static void SetFocus(GameState g, int playerId, string focus)
    {
        var player = g.Squad.Concat(g.Academy).FirstOrDefault(p => p.Id == playerId) ?? throw new GameRuleException("Atleta não pertence ao seu elenco.");
        if (focus != "Equipe" && !Attributes.Contains(focus) || focus == "Goleiro" && player.Position != "GOL") throw new GameRuleException("Escolha um foco válido para a posição do atleta.");
        Upgrade(g); g.Development[playerId].Focus = focus;
    }
    static int Overall(Player p, PlayerDevelopment profile)
    {
        int[] weights = p.Position switch { "GOL" => [1, 0, 1, 2, 6], "ZAG" => [2, 1, 5, 2, 0], "LAT" => [3, 1, 3, 3, 0], "ATA" => [2, 5, 1, 2, 0], _ => [5, 2, 1, 2, 0] };
        return (int)Math.Clamp(Math.Round(Attributes.Select((a, i) => profile.Attributes[a] * weights[i]).Sum() / 10.0, MidpointRounding.AwayFromZero), 1, 99);
    }
    // Aplicado uma vez antes da partida: o snapshot já inclui condição e habilidade treinadas.
    public static void BeforeRound(GameState g)
    {
        Upgrade(g);
        if (g.LastTrainingSeason == g.Season && g.LastTrainingRound == g.Round) throw new GameRuleException("O treino desta rodada já foi aplicado.");
        List<TrainingPlayerReport> reports = [];
        foreach (var p in Players(g).Where(p => g.Squad.Contains(p) || g.Academy.Contains(p) || g.OpponentSquads.Values.Any(s => s.Contains(p))))
        {
            bool managed = g.Squad.Contains(p) || g.Academy.Contains(p); var plan = managed ? g.WeeklyTraining : new TrainingPlan(); var profile = g.Development[p.Id];
            int beforeFitness = p.Fitness, beforeRating = p.Rating; List<AttributeGain> gains = []; Dictionary<string, int> earned = [];
            if (p.InjuryDays == 0)
            {
                int work = plan.Days.Count(Attributes.Contains); int rest = plan.Days.Count(d => d == "Descanso"), recovery = plan.Days.Count(d => d == "Recuperação");
                int cost = plan.Intensity == "Alta" ? 3 : plan.Intensity == "Leve" ? 1 : 2;
                double multiplier = plan.Intensity == "Alta" ? 1.25 : plan.Intensity == "Leve" ? .75 : 1;
                int agePoints = p.Age <= 21 ? 8 : p.Age <= 26 ? 6 : p.Age <= 30 ? 4 : 2;
                foreach (var session in plan.Days.Where(Attributes.Contains))
                {
                    var attribute = managed && profile.Focus != "Equipe" ? profile.Focus : session;
                    // Sessões coletivas de goleiros não dão XP a atletas de linha e vice-versa.
                    if (attribute == "Goleiro" && p.Position != "GOL") continue;
                    if (p.Position == "GOL" && managed && profile.Focus == "Equipe" && session is "Finalização" or "Defesa") attribute = "Goleiro";
                    if (!managed && p.Position == "GOL" && session is "Finalização" or "Defesa") attribute = "Goleiro";
                    if (profile.Attributes[attribute] >= 99) { profile.Experience[attribute] = 0; continue; }
                    int xp = Math.Max(1, (int)Math.Round(agePoints * (managed?1+ClubLife.Quality(g,"Preparador")/500.0:1) * multiplier * (.5 + beforeFitness / 200.0), MidpointRounding.AwayFromZero));
                    earned[attribute] = earned.GetValueOrDefault(attribute) + xp; profile.Experience[attribute] += xp;
                    if (profile.Experience[attribute] >= 100)
                    {
                        int old = profile.Attributes[attribute]; profile.Attributes[attribute] = Math.Min(Math.Max(old,g.Life?.HiddenPotential.GetValueOrDefault(p.Id,99)??99), old + profile.Experience[attribute] / 100); profile.Experience[attribute] %= 100;
                        if (profile.Attributes[attribute] == 99) profile.Experience[attribute] = 0;
                        gains.Add(new(attribute, old, profile.Attributes[attribute]));
                    }
                }
                p.Fitness = Math.Clamp(beforeFitness + rest * 2 + recovery - work * cost, 25, 100);
                // A primeira evolução preserva a habilidade original e soma só o ganho ponderado.
                var beforeProfile = new PlayerDevelopment { Attributes = new(profile.Attributes) };
                foreach (var gain in gains) beforeProfile.Attributes[gain.Attribute] = gain.Before;
                p.Rating = Math.Clamp(beforeRating + Overall(p, profile) - Overall(p, beforeProfile), 1, Math.Max(beforeRating,g.Life?.HiddenPotential.GetValueOrDefault(p.Id,99)??99));
            }
            if (managed) reports.Add(new(p.Id, p.Name, p.InjuryDays > 0, beforeFitness, p.Fitness, beforeRating, p.Rating, earned, gains, g.Academy.Contains(p) ? "Base" : "Profissional"));
        }
        g.TrainingReports.Insert(0, new(g.Season, g.Round, new() { Days = [.. g.WeeklyTraining.Days], Intensity = g.WeeklyTraining.Intensity }, reports));
        if (g.TrainingReports.Count > 12) g.TrainingReports.RemoveRange(12, g.TrainingReports.Count - 12);
        g.LastTrainingSeason = g.Season; g.LastTrainingRound = g.Round;
        int improved = reports.Count(r => r.Gains.Count > 0);
        if (improved > 0) g.News.Insert(0, $"Treinamento: {improved} atleta(s) evoluíram atributos antes da rodada {g.Round}.");
    }
}

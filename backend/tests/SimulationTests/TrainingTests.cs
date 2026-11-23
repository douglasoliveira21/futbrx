using System.Text.Json;
using Brasileirao;

public static class TrainingTests
{
    static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static void Reject(Action action, string text) { try { action(); throw new Exception("Aceitou: " + text); } catch (GameRuleException) { Console.WriteLine("PASS: " + text); } }
    public static void Run()
    {
        var g = Game.New("pal"); var p = g.Squad.First(x => x.Position == "MEI"); var d = g.Development[p.Id];
        Check(g.TrainingVersion == 1 && Training.Attributes.All(a => d.Attributes[a] == p.Rating) && d.Experience.Values.All(x => x == 0), "Treino inicial preserva os atributos fundamentais e inicia XP em zero");
        var detailedBefore=FootballSkills.Detailed.ToDictionary(a=>a,a=>d.Attributes[a]);
        var oldRating = p.Rating; var oldFitness = p.Fitness;
        Training.SavePlan(g, new() { Days = ["Passe", "Passe", "Passe", "Passe", "Passe", "Descanso"], Intensity = "Alta" });
        Training.SetFocus(g, p.Id, "Finalização");
        Check(p.Rating == oldRating && p.Fitness == oldFitness && d.Experience.Values.Sum() == 0, "Salvar plano e foco não concede XP nem condição");
        var snapshot = JsonSerializer.Serialize(g.WeeklyTraining);
        Reject(() => Training.SavePlan(g, new() { Days = ["Passe"] }), "Plano exige seis dias");
        Reject(() => Training.SavePlan(g, new() { Days = ["Passe", "Passe", "Passe", "Passe", "Passe", "Passe"] }), "Plano exige um dia de descanso ou recuperação");
        Reject(() => Training.SavePlan(g, new() { Days = ["Passe", "Passe", "Passe", "Passe", "Passe", "Inválido"] }), "Sessão inválida é rejeitada");
        Reject(() => Training.SavePlan(g, new() { Intensity = "Extrema" }), "Intensidade inválida é rejeitada");
        Reject(() => Training.SavePlan(g, null!), "Plano nulo é rejeitado");
        Check(JsonSerializer.Serialize(g.WeeklyTraining) == snapshot, "Rejeição preserva plano anterior");
        Reject(() => Training.SetFocus(g, p.Id, "Goleiro"), "Atleta de linha não recebe foco de goleiro");
        Reject(() => Training.SetFocus(g, g.Market[0].Id, "Passe"), "Foco exige atleta do próprio elenco");
        g.Life!.HiddenPotential[p.Id]=99; p.Age = 18; p.Fitness = 100; d.Attributes["Finalização"] = 80; d.Experience["Finalização"] = 95;
        var injured = g.Squad.First(x => x.Id != p.Id); injured.InjuryDays = 7; var injuredBefore = JsonSerializer.Serialize(g.Development[injured.Id]); var injuredFitness = injured.Fitness;
        var suspended = g.Squad.Last(x => x.Id != p.Id); suspended.SuspensionMatches = 1;
        Training.BeforeRound(g); var report = g.TrainingReports[0].Players.Single(r => r.PlayerId == p.Id);
        Check(detailedBefore.All(a=>d.Attributes[a.Key]==a.Value),"Treino de finalização preserva as habilidades detalhadas já existentes");
        Check(d.Attributes["Finalização"] == 81 && d.Experience["Finalização"] == (95+report.Experience["Finalização"])%100 && report.Experience["Finalização"] == 5*(int)Math.Round(8*1.25*(1+ClubLife.Quality(g,"Preparador")/500.0),MidpointRounding.AwayFromZero), "100 XP vira um ponto e preserva o restante; foco redireciona sessões");
        Check(p.Fitness == 87 && report.FitnessBefore == 100 && report.FitnessAfter == 87, "Carga alta consome condição antes da partida uma única vez");
        Check(JsonSerializer.Serialize(g.Development[injured.Id]) == injuredBefore && injured.Fitness == injuredFitness, "Lesionado não recebe XP nem carga de treino");
        Check(g.TrainingReports[0].Players.Single(r => r.PlayerId == suspended.Id).Experience.Values.Sum() > 0, "Suspenso pode treinar");
        var applied = JsonSerializer.Serialize(g, Game.Json); Reject(() => Training.BeforeRound(g), "Não permite aplicar a mesma semana duas vezes"); Check(JsonSerializer.Serialize(g, Game.Json) == applied, "Aplicação repetida não concede evolução adicional");
        var recovery = Game.New("pal"); var r = recovery.Squad[0]; r.Fitness = 70; Training.SetPreset(recovery, "Recuperação"); Training.BeforeRound(recovery); Check(r.Fitness > 70, "Modelo de recuperação repõe condição");
        var young = Game.New("pal"); var y = young.Squad[2]; var older = young.Squad[3]; y.Age = 18; older.Age = 34; y.Fitness = older.Fitness = 100; Training.BeforeRound(young);
        Check(young.TrainingReports[0].Players.Single(r => r.PlayerId == y.Id).Experience.Values.Sum() > young.TrainingReports[0].Players.Single(r => r.PlayerId == older.Id).Experience.Values.Sum(), "Atleta jovem evolui mais rápido com a mesma carga");
        var limit = Game.New("pal"); var max = limit.Squad[0]; foreach (var a in Training.Attributes) limit.Development[max.Id].Attributes[a] = 99; Training.BeforeRound(limit);
        Check(Training.Attributes.All(a=>limit.Development[max.Id].Attributes[a]==99) && limit.Development[max.Id].Attributes.Values.All(x=>x<=99) && limit.Development[max.Id].Experience.Values.All(x => x == 0), "Atributos não ultrapassam 99 nem acumulam XP no teto");
        var legacy = Game.New("pal"); legacy.TrainingVersion = 0; legacy.Development.Clear(); legacy.Training = "Recuperação"; var ratings = legacy.Squad.Select(p => p.Rating).ToArray(); Training.Upgrade(legacy); Training.Upgrade(legacy);
        Check(legacy.WeeklyTraining.Intensity == "Leve" && legacy.TrainingReports.Count == 0 && legacy.Squad.Select(p => p.Rating).SequenceEqual(ratings), "Save legado mantém foco e habilidade sem treino retroativo");
        for (int i = 0; i < 14; i++) { recovery.Round++; Training.BeforeRound(recovery); }
        Check(recovery.TrainingReports.Count == 12, "Relatórios têm retenção limitada a doze semanas");
        var replayGame = Game.New("pal"); Game.Advance(replayGame); var f = replayGame.Fixtures.First(x => x.HomeGoals != null); var replay = JsonSerializer.Serialize(MatchSimulation.Run(f.Setup!, f.Seed, true));
        Game.Advance(replayGame); Check(JsonSerializer.Serialize(MatchSimulation.Run(f.Setup!, f.Seed, true)) == replay, "Treinos posteriores não alteram replay de partida já disputada");
        var saved = Game.Read(new() { State = JsonSerializer.Serialize(replayGame, Game.Json) }); Check(saved.Development[p.Id].Attributes.SequenceEqual(replayGame.Development[p.Id].Attributes) && saved.TrainingReports.Count == 2, "Save conserva atributos, XP e relatórios");
        var market = Game.New("pal"); var free = market.Market[0]; var development = market.Development[free.Id]; development.Attributes["Passe"] = 88; development.Experience["Passe"] = 37;
        var quote = TransferMarket.Quote(market, free.Id, "inbound", 3); var offer = TransferMarket.Propose(market, free.Id, "inbound", 0, quote.Wage, 3); TransferMarket.Accept(market, offer.Id);
        Check(market.Squad.Contains(free) && market.Development[free.Id].Attributes["Passe"] == 88 && market.Development[free.Id].Experience["Passe"] == 37, "Contratação conserva atributos e XP do atleta");
        var oldReports = replayGame.TrainingReports.Count; var oldSeason = replayGame.Season; replayGame.Round = 39; replayGame.Life!.Competitions.Clear();TransferMarket.NextSeason(replayGame); Training.BeforeRound(replayGame);
        Check(replayGame.TrainingReports.Count == oldReports + 1 && replayGame.TrainingReports[0].Season == oldSeason + 1 && replayGame.TrainingReports[0].Round == 1, "Nova temporada permite treino e conserva relatórios anteriores");
        Console.WriteLine("Testes de treino semanal e evolução passaram.");
    }
}


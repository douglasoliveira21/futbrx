using System.Text.Json;
using Brasileirao;

public static class SportsTests
{
    static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    static void Reject(Action action, string text) { try { action(); throw new Exception("Aceitou: " + text); } catch (GameRuleException) { Console.WriteLine("PASS: " + text); } }
    public static void Run()
    {
        var swapGame = Game.New("pal");
        var swapOut = swapGame.Squad.First(p => p.Starter && p.Position != "GOL");
        var swapIn = swapGame.Squad.First(p => !p.Starter && p.Position != "GOL");
        Sports.Swap(swapGame, swapOut.Id, swapIn.Id);
        Check(!swapOut.Starter && swapIn.Starter && swapGame.Squad.Count(p => p.Starter) == 11, "Troca na prancheta mantém onze titulares em uma operação");
        Reject(() => Sports.Swap(swapGame, swapOut.Id, swapIn.Id), "Troca repetida não duplica ou remove titulares");
        var goalkeeper = swapGame.Squad.First(p => p.Starter && p.Position == "GOL");
        Reject(() => Sports.Swap(swapGame, goalkeeper.Id, swapOut.Id), "Prancheta protege a posição do goleiro");
        var g = Game.New("pal"); var team = Sports.BuildTeam(g, g.ClubId); var rival = Sports.BuildTeam(g, g.Clubs.First(c => c.Id != g.ClubId).Id);
        var outgoing = team.Players.First(p => p.Position != "GOL"); var incoming = team.Bench!.First(p => p.Position != "GOL");
        Sports.SavePlan(g, [new(1, outgoing.Id, incoming.Id)], false, false);
        var setup = new MatchSetup(MatchSimulation.Version, Sports.BuildTeam(g, g.ClubId), rival with { AutoSubstitute = false });
        var replay = MatchSimulation.Run(setup, 42, true); var summary = MatchSimulation.Run(setup, 42, false);
        var substitution = replay.Events.Single(e => e.Type == "substitution" && e.Team == g.ClubId);
        Check(substitution.Player == outgoing.Id && substitution.RelatedPlayer == incoming.Id && substitution.Second >= 60, "Troca programada preserva identidades de saída e entrada");
        Check(replay.Frames.Any(f => f.Second >= substitution.Second && f.Players.Any(p => p.Id == incoming.Id && p.Active)) && replay.Frames.Where(f => f.Second >= substitution.Second).All(f => f.Players.All(p => p.Id != outgoing.Id || !p.Active)), "Reserva aparece nos frames e substituído deixa de atuar");
        Check(replay.PlayerStats!.Single(p => p.PlayerId == outgoing.Id).Minutes <= 2 && replay.PlayerStats!.Single(p => p.PlayerId == incoming.Id).Minutes > 0, "Minutos são atribuídos ao jogador correto após a troca");
        Check(JsonSerializer.Serialize(replay.Events) == JsonSerializer.Serialize(summary.Events) && JsonSerializer.Serialize(replay.PlayerStats) == JsonSerializer.Serialize(summary.PlayerStats), "Resumo e replay compartilham eventos e estatísticas");
        Check(replay.Events.Any(e => e.Type == "second_half" && e.Second >= 2700), "Intervalo troca o lado das equipes");
        Reject(() => Sports.SavePlan(g, [new(0, outgoing.Id, incoming.Id)], true, true), "Minuto inválido é rejeitado");
        Reject(() => Sports.SavePlan(g, [new(60, outgoing.Id, incoming.Id), new(70, outgoing.Id, incoming.Id)], true, true), "Não permite reutilizar jogador no plano");
        Reject(() => Sports.SavePlan(g, [null!], true, true), "Plano nulo é rejeitado sem erro interno");
        Reject(() => Sports.SavePlan(g, [new(60, team.Players[0].Id, incoming.Id)], true, true), "Troca de goleiro exige goleiro reserva");
        Check(g.Substitutions.Count == 1 && g.Substitutions[0].Minute == 1, "Plano inválido conserva o plano anterior");
        var p = g.Squad.First(x => x.Id == outgoing.Id); p.YellowCards = 2;
        var fixture = new Fixture { Home = g.ClubId, Away = rival.Id, Events = [new(30, "yellow_card", g.ClubId, p.Id, "Amarelo", 1800), new(60, "injury", g.ClubId, p.Id, "Lesão", 3600, null, 7)] };
        Sports.AfterRound(g, [fixture]);
        Check(p.YellowCards == 0 && p.SuspensionMatches == 1 && p.InjuryDays == 7 && !Sports.Eligible(p), "Terceiro amarelo suspende e lesão nova não recupera na mesma rodada");
        Sports.AfterRound(g, []); Check(Sports.Eligible(p) && p.InjuryDays == 0 && p.SuspensionMatches == 0, "Rodada seguinte cumpre punição e recupera sete dias");
        p.SuspensionMatches = 1; p.Starter = true; g.Substitutions = []; g.AutoReplaceUnavailable = false;
        Reject(() => Sports.Prepare(g), "Escalação manual bloqueia titular suspenso"); g.AutoReplaceUnavailable = true; Sports.Prepare(g); Check(!p.Starter && g.Squad.Count(x => x.Starter) == 11, "Escalação automática recompõe suspensos");
        var legacy = setup with { EngineVersion = LegacyPositionalV1.Version };
        Check(JsonSerializer.Serialize(MatchSimulation.Run(legacy, 42, true)) == JsonSerializer.Serialize(LegacyPositionalV1.Run(legacy, 42, true)), "Replays v1 seguem o motor legado preservado");
        bool sawRed = false, sawInjury = false;
        for (int seed = 1; seed <= 80; seed++)
        {
            var run = MatchSimulation.Run(setup with { Home = setup.Home with { AutoSubstitute = true }, Away = setup.Away with { AutoSubstitute = true } }, seed, true);
            Check(run.Events.Where(e => e.Type == "substitution").GroupBy(e => e.Team).All(x => x.Count() <= 5), $"Seed {seed}: respeita limite de cinco trocas");
            Check(run.Frames.All(f => f.Players.Where(p => p.Active).Select(p => p.Id).Distinct().Count() == f.Players.Count(p => p.Active)), "Frames não duplicam atletas ativos");
            foreach (var red in run.Events.Where(e => e.Type == "red_card")) { sawRed = true; Check(run.Frames.Where(f => f.Second >= red.Second).All(f => f.Players.All(p => !p.Active || p.Id != red.Player)), "Expulso permanece fora dos frames ativos"); }
            sawInjury |= run.Events.Any(e => e.Type == "injury");
            if (sawRed && sawInjury) break;
        }
        Check(sawRed && sawInjury, "Amostra determinística cobre expulsão e lesão");
        var old = Game.New("pal"); old.Round = 12; old.SportsVersion = 0; Sports.Upgrade(old); Check(old.PlayerStatsFromRound == 12, "Save legado registra início real da cobertura estatística");
        Console.WriteLine("Testes de disponibilidade, disciplina e substituições passaram.");
    }
}

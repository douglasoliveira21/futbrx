using Brasileirao;
using System.Text.Json;
public static class LiveControlsTests
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
    static void Reject(Action action,string text){try{action();}catch(GameRuleException){Check(true,text);return;}throw new Exception(text);}
    public static void Run()
    {
        var g=Game.New("pal");
        // Pin the generated calendar, personalities and AI tactics: random careers
        // should not change which rare refereeing situations this test covers.
        g.LotterySeed="live-controls-regression";g.Fixtures=SeasonCalendar.Generate(g.Clubs,g.Season,g.LotterySeed);g.Life=null;ClubLife.Upgrade(g);
        var now=DateTimeOffset.UtcNow;var live=LiveBroadcast.Start(g,now);
        using (var pauseRuntime = new LiveSimulation())
        {
            var paused = JsonSerializer.Deserialize<LiveMatch>(JsonSerializer.Serialize(live, Game.Json), Game.Json)!;
            var opening = pauseRuntime.Advance(paused);
            LiveMatchControls.Apply(paused, opening, "arm_stoppage", 12, "", "", 0, 0, now);
            paused.WatchedSeconds = LiveBroadcast.EntranceSeconds + 2698.0 / LiveBroadcast.Speed;
            var stopped = pauseRuntime.Advance(paused);
            var stopSecond = paused.SimulatedSecond;
            Check(paused.StoppagePaused && !paused.HalfTimeAwaiting && stopSecond < 2698
                && stopped.Events.Any(e => e.Second == stopSecond && e.Type is "throw_in" or "goal" or "corner" or "free_kick" or "goal_kick" or "offside" or "penalty"),
                "Pausa automática ocorre na primeira interrupção, sem avançar até o fim do crédito");
            Check(stopped.Frames.Last().Possession is 0 or 1 && stopped.Frames.Last().Players.All(p => p.Fitness.HasValue),
                "A transmissão inclui a posse e a condição calculada no motor");
            LiveBroadcast.Tick(paused, now.AddSeconds(1), true);
            LiveBroadcast.Tick(paused, now.AddSeconds(2), true);
            using var rebuilt = new LiveSimulation();
            var restoredPause = rebuilt.Advance(paused);
            Check(paused.StoppagePaused && paused.SimulatedSecond == stopSecond && restoredPause.Frames.Last().Second == stopped.Frames.Last().Second,
                "O relógio permanece parado mesmo após reconstruir a transmissão");
            LiveMatchControls.Apply(paused, restoredPause, "resume_stoppage", 12, "", "", 0, 0, now.AddSeconds(2));
            LiveMatchControls.Apply(paused, restoredPause, "tactics", 12, "4-3-3", "Ataque total", 0, 0, now.AddSeconds(2),
                new(Tempo: "Alto", DefensiveTransition: "Pressionar após perda"));
            paused.WatchedSeconds += 4;
            var resumed = rebuilt.Advance(paused);
            Check(!paused.StoppagePaused && paused.SimulatedSecond > stopSecond && resumed.Events.Any(e => e.Type == "tactics_change" && e.Text.Contains("Ataque total")),
                "Retomada registra a nova mentalidade após a pausa");
        }
        Check(live.Fixture.Setup!.EngineVersion==MatchSimulation.Version,"Novas partidas usam motor com comandos ao vivo");
        var shared=LiveBroadcast.Start(Game.New("pal"),now);LiveBroadcast.Tick(shared,now,true,"watching");LiveBroadcast.Tick(shared,now.AddSeconds(.5),false,"paused");LiveBroadcast.Tick(shared,now.AddSeconds(1),true,"watching");
        Check(shared.WatchedSeconds==1,"Aba pausada não congela outra aba ativa nem duplica velocidade");
        using var runtime=new LiveSimulation();var replay=runtime.Advance(live);
        var savedPreset=TacticalPlans.Save(g,"","Plano para a partida","3241","Ofensiva",new());
        var currentTeam=live.ManagedTeam==live.Fixture.Setup!.Home.Id?live.Fixture.Setup.Home:live.Fixture.Setup.Away;
        var matchPlan=TacticalPlans.MaterializeLive(savedPreset,currentTeam,replay);
        Check(matchPlan.Positions?.Count==11&&g.TacticalPlan.Positions==null,"Plano salvo produz onze posições para a partida sem alterar o plano permanente");
        var originalInstructions=live.Fixture.Setup.Instructions;
        LiveMatchControls.Apply(live,replay,"tactics",12,currentTeam.Formation,savedPreset.Mentality,0,0,now,matchPlan);
        Check(live.Fixture.Setup.Instructions!.Last().Plan!.Positions!.Count==11,"Comando de plano é registrado no snapshot da partida");
        live.Fixture.Setup=live.Fixture.Setup with{Instructions=originalInstructions};
        LiveMatchControls.Apply(live,replay,"speed",48,"","",0,0,now);
        Reject(()=>LiveMatchControls.Apply(live,replay,"speed",999,"","",0,0,now),"Multiplicador arbitrário é rejeitado");
        live.WatchedSeconds=12;LiveBroadcast.Tick(live,now,true);LiveBroadcast.Tick(live,now.AddSeconds(1),true);
        Check(live.WatchedSeconds==16,"48× credita 48 segundos de partida por segundo real");
        replay=runtime.Advance(live);var past=JsonSerializer.Serialize(replay.Frames,Game.Json);
        var team=live.ManagedTeam==live.Fixture.Setup.Home.Id?live.Fixture.Setup.Home:live.Fixture.Setup.Away;
        var outgoing=team.Players.First(p=>p.Position!="GOL");var incoming=team.Bench!.First(p=>p.Position!="GOL");
        var otherOut=team.Players.First(p=>p.Position!="GOL"&&p.Id!=outgoing.Id);var otherIn=team.Bench!.First(p=>p.Position!="GOL"&&p.Id!=incoming.Id);
        Reject(()=>LiveMatchControls.ApplyBatch(live,replay,[new(outgoing.Id,incoming.Id),new(otherOut.Id,incoming.Id)],now),"Lote inválido não confirma parcialmente a primeira troca");
        Check((live.Fixture.Setup.Instructions?.Count??0)==0,"Lote rejeitado mantém a fila original");
        LiveMatchControls.ApplyBatch(live,replay,[new(outgoing.Id,incoming.Id),new(otherOut.Id,otherIn.Id)],now);
        Check(live.Fixture.Setup.Instructions!.Count==2&&live.Fixture.Setup.Instructions.All(c=>c.Second==live.SimulatedSecond+2),"Múltiplas trocas compartilham o mesmo instante e a mesma parada");
        LiveMatchControls.Apply(live,replay,"cancel_substitution",12,"","",otherOut.Id,otherIn.Id,now);
        Check(live.Fixture.Setup.Instructions!.Count==1,"Cancelar remove apenas a troca pendente solicitada");
        Reject(()=>LiveMatchControls.Apply(live,replay,"cancel_substitution",12,"","",otherOut.Id,otherIn.Id,now),"Troca removida não pode ser cancelada novamente");
        live.Fixture.Setup=live.Fixture.Setup with{Instructions=[]};
        LiveMatchControls.Apply(live,replay,"substitution",12,"","",outgoing.Id,incoming.Id,now);
        Reject(()=>LiveMatchControls.Apply(live,replay,"substitution",12,"","",outgoing.Id,incoming.Id,now),"Não duplica reserva ou saída pendente");
        LiveMatchControls.Apply(live,replay,"tactics",12,"3-5-2","Ofensiva",0,0,now);
        Check(JsonSerializer.Serialize(runtime.Advance(live).Frames,Game.Json)==past,"Comandos não alteram frames anteriores");
        live.WatchedSeconds=1000;replay=runtime.Advance(live);
        Check(live.HalfTimeAwaiting&&!live.HalfTimeAccepted&&!replay.Events.Any(e=>e.Type=="second_half"),"Motor para no intervalo sem calcular segundo tempo");
        Check(replay.Events.Any(e=>e.Type=="substitution"&&e.Player==outgoing.Id&&e.RelatedPlayer==incoming.Id)&&replay.Events.Any(e=>e.Type=="tactics_change"),"Banco e tática afetam a simulação em andamento");
        var frozen=live.SimulatedSecond;var watched=live.WatchedSeconds;
        LiveBroadcast.Tick(live,now.AddSeconds(2),true);runtime.Advance(live);
        Check(live.SimulatedSecond==frozen&&live.WatchedSeconds==watched,"Intervalo permanece parado até comando explícito");
        var restored=JsonSerializer.Deserialize<LiveMatch>(JsonSerializer.Serialize(live,Game.Json),Game.Json)!;
        using(var rebuilt=new LiveSimulation())Check(JsonSerializer.Serialize(rebuilt.Advance(restored),Game.Json)==JsonSerializer.Serialize(replay,Game.Json),"Reinício conserva comandos e intervalo");
        var halfOut=replay.Frames[^1].Players.First(p=>p.Team==(team.Id==live.Fixture.Setup.Home.Id?0:1)&&p.Active&&p.Position!="GOL"&&p.Id!=incoming.Id);
        var halfIn=team.Bench!.First(p=>p.Position!="GOL"&&p.Id!=incoming.Id&&!replay.Events.Any(e=>e.Type=="substitution"&&e.RelatedPlayer==p.Id));
        LiveMatchControls.Apply(live,replay,"substitution",12,"","",halfOut.Id,halfIn.Id,now);
        Check(live.Fixture.Setup.Instructions!.Last().AtHalfTime,"Troca no intervalo fica identificada sem consumir parada");
        LiveMatchControls.Apply(live,replay,"second_half",12,"","",0,0,now);
        live.WatchedSeconds+=1000;replay=runtime.Advance(live);
        Check(live.EngineFinished&&replay.Events.Any(e=>e.Type=="second_half")&&replay.Events.Any(e=>e.Type=="fulltime"),"Botão do intervalo libera segundo tempo até apito final");
        Check(JsonSerializer.Serialize(replay,Game.Json)==JsonSerializer.Serialize(MatchSimulation.Run(live.Fixture.Setup,live.Fixture.Seed,true),Game.Json),"Replay final reproduz todos os comandos determinísticos");
        var saved=LiveBroadcast.Finish(g,live.Id);var fixture=saved.Fixtures.Single(f=>f.Round==1&&(f.Home==g.ClubId||f.Away==g.ClubId));
        Check(fixture.Setup!.Instructions?.Count==3&&fixture.HomeGoals==replay.HomeGoals&&fixture.AwayGoals==replay.AwayGoals,"Salvar resultado conserva instruções e placar");
        Check(TacticalPlans.ForClub(saved).Any(plan => plan.Id == savedPreset.Id), "Salvar resultado preserva os planos criados durante a partida");
        int throws=0,offside=0,penalties=0,cards=0;
        for(int seed=1;seed<=32;seed++){
            var match=MatchSimulation.Run(live.Fixture.Setup with{Instructions=null},seed,true);
            throws+=match.Events.Count(e=>e.Type=="throw_in");offside+=match.Events.Count(e=>e.Type=="offside");penalties+=match.Events.Count(e=>e.Type=="penalty");cards+=match.Events.Count(e=>e.Type=="yellow_card"||e.Type=="red_card");
            Check(match.Frames.All(f=>double.IsFinite(f.Ball.X)&&double.IsFinite(f.Ball.Y)&&double.IsFinite(f.Ball.Z))&&match.Events.All(e=>e.Second<=match.Duration),"Bola finita e lances dentro da duração, semente "+seed);
        }
        Check(throws>0&&penalties>0&&cards>0,$"Lances ativos: {throws} laterais, {offside} impedimentos, {penalties} pênaltis, {cards} cartões");
    }
}

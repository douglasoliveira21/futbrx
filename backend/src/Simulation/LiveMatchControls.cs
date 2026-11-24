namespace Brasileirao;

public static class LiveMatchControls
{
    public static readonly int[] Speeds = [1, 2, 4, 6, 8, 12, 24, 48, 96];
    public static void ApplyBatch(LiveMatch live,MatchReplay replay,IReadOnlyList<SubstitutionCommand>? substitutions,DateTimeOffset now)
    {
        if(substitutions==null||substitutions.Count is <1 or >5)throw new GameRuleException("Informe de uma a cinco substituições.");
        var setup=live.Fixture.Setup;
        try { foreach(var change in substitutions){if(change==null)throw new GameRuleException("Substituição inválida.");Apply(live,replay,"substitution",12,"","",change.OutPlayerId,change.InPlayerId,now);} }
        catch {live.Fixture.Setup=setup;throw;}
    }
    // The coach decided about the injured player: the broadcast can go on.
    static void ResolveInjury(LiveMatch live, int playerId)
    {
        if (!live.InjuryAwaiting || live.InjuryPlayerId != playerId) return;
        live.InjuryResolved.Add(playerId); live.InjuryAwaiting = false; live.InjuryPlayerId = 0;
    }
    public static void Apply(LiveMatch live, MatchReplay replay, string type, int speed, string formation, string mentality, int outgoing, int incoming, DateTimeOffset now,TacticalPlan? plan=null)
    {
        var setup=live.Fixture.Setup!;
        if(!MatchSimulation.SupportsControls(setup.EngineVersion))throw new GameRuleException("Os controles em campo estão disponíveis nas novas partidas.");
        if(live.EngineFinished)throw new GameRuleException("A partida já terminou.");
        live.LastHeartbeat=now;live.WasPlaying=false;live.Viewers.Clear();
        if (type == "arm_stoppage") { live.PauseAfterSecond = live.SimulatedSecond; return; }
        if (type == "cancel_stoppage") { live.PauseAfterSecond = null; return; }
        if (type == "resume_stoppage") { live.StoppagePaused = false; return; }
        if(type=="speed") { if(!Speeds.Contains(speed))throw new GameRuleException("Velocidade inválida.");live.PlaybackSpeed=speed;return; }
        if(type=="second_half") { if(!live.HalfTimeAwaiting)throw new GameRuleException("A partida não está no intervalo.");live.HalfTimeAccepted=true;live.HalfTimeAwaiting=false;return; }
        var commands=setup.Instructions?.ToList() ?? [];
        var second=live.SimulatedSecond+2;
        var team=live.ManagedTeam==setup.Home.Id?setup.Home:setup.Away;
        if(type=="cancel_substitution")
        {
            var pendingCommand=commands.FindIndex(c=>c.Team==team.Id&&c.Type=="substitution"&&c.OutPlayerId==outgoing&&c.InPlayerId==incoming&&!replay.Events.Any(e=>e.Team==team.Id&&(e.Type is "substitution" or "substitution_cancelled")&&e.Player==c.OutPlayerId&&e.RelatedPlayer==c.InPlayerId));
            if(pendingCommand<0)throw new GameRuleException("A troca já foi executada, cancelada ou não está na fila.");
            commands.RemoveAt(pendingCommand);live.Fixture.Setup=setup with{Instructions=commands};return;
        }
        if(type=="tactics")
        {
            if(!Tactics.Formations.Contains(formation)||!(setup.EngineVersion is PositionalV10.Version or PositionalV11.Version or PositionalV12.Version or PositionalV13.Version or PositionalV14.Version or PositionalV15.Version ? Tactics.Mentalities : Tactics.LegacyMentalities).Contains(mentality))throw new GameRuleException("Estratégia inválida.");
            if(setup.EngineVersion is not (PositionalV9.Version or PositionalV10.Version or PositionalV11.Version or PositionalV12.Version or PositionalV13.Version or PositionalV14.Version or PositionalV15.Version)&&(plan!=null||formation=="4-2-3-1"))throw new GameRuleException("As instruções avançadas estão disponíveis nas novas partidas.");
            if(plan!=null)
            {
                // O plano da carreira pode citar atletas fora dos relacionados; eles não participam da partida.
                var roster=team.Players.Concat(team.Bench??[]).Select(p=>p.Id).ToHashSet();
                plan=plan with{Roles=plan.Roles?.Where(r=>roster.Contains(r.Key)).ToDictionary(),Positions=plan.Positions?.Where(r=>roster.Contains(r.Key)).ToDictionary()};
                Tactics.ValidatePlan(plan,roster);
            }
            commands.Add(new(second,team.Id,"tactics",formation,mentality,AtHalfTime:live.HalfTimeAwaiting,Plan:plan));
        }
        else if(type=="substitution")
        {
            var teamIndex=team.Id==setup.Home.Id?0:1;
            var players=replay.Frames.Last().Players.Where(p=>p.Team==teamIndex&&p.Active).ToList();
            var replaced=replay.Events.Where(e=>e.Team==team.Id&&e.Type=="substitution").ToList();
            var pending=commands.Where(c=>c.Team==team.Id&&c.Type=="substitution"&&!replay.Events.Any(e=>e.Team==team.Id&&(e.Type is "substitution" or "substitution_cancelled") &&e.Player==c.OutPlayerId&&e.RelatedPlayer==c.InPlayerId)).ToList();
            var used=replaced.Select(e=>e.RelatedPlayer).Concat(pending.Select(c=>(int?)c.InPlayerId)).ToHashSet();
            var old=players.Find(p=>p.Id==outgoing);var fresh=team.Bench?.Find(p=>p.Id==incoming);
            if(old==null||fresh==null||pending.Any(c=>c.OutPlayerId==outgoing)||used.Contains(incoming))throw new GameRuleException("Selecione um atleta em campo e um reserva ainda não utilizado.");
            if((old.Position=="GOL")!=(fresh.Position=="GOL"))throw new GameRuleException("Troque o goleiro por um reserva goleiro.");
            if(replaced.Count+pending.Count>=5)throw new GameRuleException("Limite de cinco substituições atingido.");
            var half=replay.Events.FirstOrDefault(e=>e.Type=="second_half")?.Second;
            var stops=replaced.Where(e=>half==null||e.Second!=half&&e.Second!=half+2).Select(e=>e.Second).Concat(pending.Where(c=>!c.AtHalfTime).Select(c=>c.Second)).Distinct().Count();
            bool injurySwap=replay.Events.Any(e=>e.Type=="injury"&&e.Team==team.Id&&e.Player==outgoing);
            if(!live.HalfTimeAwaiting&&!injurySwap&&stops>=3&&!pending.Any(c=>c.Second==second))throw new GameRuleException("As três paradas de substituição já foram utilizadas.");
            commands.Add(new(second,team.Id,"substitution",OutPlayerId:outgoing,InPlayerId:incoming,AtHalfTime:live.HalfTimeAwaiting));
            ResolveInjury(live,outgoing);
        }
        else if(type=="injury_retire")
        {
            // Playing on with ten: only for an injured player of the managed team whose decision is pending.
            if(!live.InjuryAwaiting||live.InjuryPlayerId!=outgoing)throw new GameRuleException("Este atleta não está aguardando decisão por lesão.");
            commands.Add(new(second,team.Id,"injury_retire",OutPlayerId:outgoing));
            ResolveInjury(live,outgoing);
        }
        else throw new GameRuleException("Comando de partida inválido.");
        live.Fixture.Setup=setup with{Instructions=commands};
    }
}
public sealed record SubstitutionCommand(int OutPlayerId,int InPlayerId);

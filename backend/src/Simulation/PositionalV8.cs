using Checkpoint=Brasileirao.PositionalV5.Checkpoint;
namespace Brasileirao;

// V7 stays frozen. V8 adapts its regulation match and calculates knockout shootouts
// only as the live coroutine advances, without adding shootout goals to league goals.
public static class PositionalV8
{
    public const string Version="positional-v8";
    public static MatchReplay Run(MatchSetup setup,int seed,bool captureFrames)=>Stream(setup,seed,captureFrames).Last().Replay;
    public static IEnumerable<Checkpoint> Stream(MatchSetup setup,int seed,bool captureFrames=true,FootballBrain? intelligence=null,bool delayedKeeper=false,bool physicalShots=false,bool continuousContacts=false,bool realisticThrowIns=false,bool throwInSupport=false,bool injuryRealism=false)
    {
        if(setup.EngineVersion!=Version)throw new GameRuleException("Versão do motor inválida.");
        MatchReplay? final=null;
        foreach(var step in PositionalV7.Stream(setup with{EngineVersion=PositionalV7.Version},seed,captureFrames,intelligence,delayedKeeper,physicalShots,continuousContacts,realisticThrowIns,throwInSupport,injuryRealism)){
            var replay=step.Replay with{EngineVersion=Version,Setup=setup};
            // A second leg is level when this match plus the goals already scored in the first leg are equal.
            if(step.Finished&&setup.Knockout&&replay.HomeGoals+setup.AggregateLead==replay.AwayGoals){final=replay;break;}
            yield return step with{Replay=replay};
        }
        if(final==null)yield break;
        var events=final.Events.Where(e=>e.Type!="fulltime").ToList();var frames=final.Frames.ToList();
        int second=final.Duration,home=0,away=0,homeTaken=0,awayTaken=0;bool done=false;
        var rng=new Random(seed^0x4863);
        void Event(string type,int team,int player,string text)=>events.Add(new((int)Math.Ceiling(second/60.0),type,team==0?setup.Home.Id:setup.Away.Id,player,text,second));
        MatchReplay Replay(int end)=>final with{Duration=end,Events=events,Frames=frames,HomePenalties=home,AwayPenalties=away};
        Event("shootout_start",0,0,"Empate no tempo regulamentar. Começa a disputa de pênaltis.");
        yield return new(second,false,Replay(second+24));
        while(!done){
            int team=homeTaken==awayTaken?0:1;var squad=team==0?setup.Home:setup.Away;
            var active=frames.LastOrDefault()?.Players.Where(p=>p.Team==team&&p.Active&&p.Position!="GOL").ToList();
            var taker=active?.ElementAtOrDefault((team==0?homeTaken:awayTaken)%Math.Max(1,active.Count))??new PlayerPose(squad.Players[1].Id,team,41.5,0,Math.PI/2,"penalty_kick",true,squad.Players[1].Name,squad.Players[1].Position);
            int start=second,contact=second+14,end=second+24;
            bool goal=rng.NextDouble()<.76;bool lastPair=homeTaken>=14&&awayTaken>=14;
            if(lastPair)goal=team==0;
            for(int tick=start+2;tick<=end;tick+=2){second=tick;
                if(captureFrames){var players=(frames.LastOrDefault()?.Players??setup.Home.Players.Select((p,i)=>new PlayerPose(p.Id,0,-5,i*2,0,"idle",true,p.Name,p.Position)).Concat(setup.Away.Players.Select((p,i)=>new PlayerPose(p.Id,1,5,i*2,0,"idle",true,p.Name,p.Position))).ToList()).Select((p,i)=>p with{X=p.Team==0?-6:6,Z=(i%11-5)*2,Yaw=Math.PI/2,Action="idle",ActionSecond=start}).ToList();for(int i=0;i<players.Count;i++){var p=players[i];if(p.Id==taker.Id)players[i]=p with{X=41.5,Z=0,Yaw=Math.PI/2,Action="penalty_kick",ActionSecond=contact};else if(p.Team!=team&&p.Position=="GOL")players[i]=p with{X=51.5,Z=0,Yaw=-Math.PI/2,Action=tick<contact?"keeper_ready":goal?"dive_left":"dive_right",ActionSecond=contact+8};}double progress=Math.Clamp((tick-contact)/10.0,0,1);frames.Add(new(second,players,new(41.5+progress*11,Math.Max(.24,Math.Sin(progress*Math.PI)*.8),progress*(goal?2.2:3.9))));}
                yield return new(second,false,Replay(end+2));
            }
            if(team==0){homeTaken++;if(goal)home++;}else{awayTaken++;if(goal)away++;}
            Event(goal?"shootout_goal":"shootout_miss",team,taker.Id,goal?taker.Name+" converte o pênalti.":taker.Name+" não converte o pênalti.");
            done=homeTaken<=5&&awayTaken<=5?(home>away+5-awayTaken||away>home+5-homeTaken):false;
            if(homeTaken==awayTaken&&homeTaken>=5&&home!=away)done=true;
            if(lastPair&&team==1)done=true;
        }
        second+=2;Event("fulltime",home>away?0:1,0,$"Fim de jogo. Pênaltis: {home} × {away}.");
        if(captureFrames&&frames.Count>0)frames.Add(frames[^1] with{Second=second});
        yield return new(second,true,Replay(second));
    }
}

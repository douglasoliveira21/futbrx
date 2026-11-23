namespace Brasileirao;

// Pure decisions over observations of the existing agents. This layer owns no
// players, physics, match clock, score or animation controllers.
public sealed record FootballObservation(int Index,MatchPlayer Player,int Team,int Slot,double X,double Z,double Yaw,double VelocityX,double VelocityZ,bool Active);
public sealed record FootballFlight(int Recipient,int Team,double TargetX,double TargetZ,int Start,int End,string Delivery,bool Shot);
public sealed record FootballContext(int Second,int HalfEnd,int Duration,int Possession,int Carrier,double BallX,double BallY,double BallZ,MatchTeam[] Teams,FootballObservation[] Players,FootballFlight? Flight,int HomeGoals,int AwayGoals);
public sealed record FootballChoice(string Kind,int Recipient,double X,double Z,double Utility,double Risk=0,string Pattern="");
public sealed record FootballTarget(double X,double Z,bool Sprint=false,string Role="");
public sealed record CoachAdjustment(string Formation,string Mentality,TacticalPlan Plan,string Reason,int OutPlayer=0,int InPlayer=0);
public sealed record FootballShot(string Result,double X,double Z,double Quality,double ReactionSeconds);

public sealed class FootballBrain(int seed)
{
    FootballContext context=null!;
    readonly Dictionary<int,int> firstSeen=[];
    readonly Dictionary<int,(int Sender,int At)> arrivals=[];
    readonly Dictionary<int,FootballTarget> carrying=[];
    readonly int[] lastCoach=[-1000,-1000];
    readonly Dictionary<int,int> lastPattern=[];
    readonly HashSet<int> pressers=[];
    int lastPossession=-1,changedAt=-1000;
    double previousBallX,previousBallZ,velocityBallX,velocityBallZ;
    public int PressingCount=>pressers.Count;
    public int LastTransition=>changedAt;
    static double Sqr(double x)=>x*x;
    public static double Distance(double ax,double az,double bx,double bz)=>Math.Sqrt(Sqr(ax-bx)+Sqr(az-bz));
    double Direction(int team)=>(team==0?1:-1)*(context.Second>=context.HalfEnd?-1:1);
    TacticalPlan Plan(int team)=>context.Teams[team].Plan??new();
    FootballObservation Owner=>context.Players[context.Carrier];
    IEnumerable<FootballObservation> Team(int team)=>context.Players.Where(p=>p.Active&&p.Team==team);
    public double Fitness(FootballObservation p)
    {
        double elapsed=(context.Second-firstSeen.GetValueOrDefault(p.Player.Id,context.Second))/60.0;
        var plan=Plan(p.Team);double work=.20+(plan.Pressure=="Alta"?.14:0)+(plan.Style=="Contra-ataque"?.04:0)+(p.Player.Position=="LAT"&&plan.Overlap?.06:0);
        return Math.Clamp(p.Player.Fitness-elapsed*work/(.65+FootballSkills.Normal(p.Player,"Resistência")*.65),15,100);
    }
    public double TopSpeed(FootballObservation p,bool sprint)=> (sprint?4.6+FootballSkills.Get(p.Player,"Velocidade")/32:3+FootballSkills.Get(p.Player,"Velocidade")/70)*(.6+.4*Fitness(p)/100);
    public double Acceleration(FootballObservation p)=>1.5+FootballSkills.Get(p.Player,"Aceleração")/32;
    public void Update(FootballContext value)
    {
        if(context!=null&&value.Second>context.Second){double dt=(value.Second-context.Second)/12.0;velocityBallX=(value.BallX-previousBallX)/dt;velocityBallZ=(value.BallZ-previousBallZ)/dt;}
        context=value;previousBallX=value.BallX;previousBallZ=value.BallZ;
        foreach(var p in value.Players.Where(p=>p.Active))firstSeen.TryAdd(p.Player.Id,value.Second);
        if(lastPossession!=value.Possession){changedAt=value.Second;lastPossession=value.Possession;carrying.Clear();}
        pressers.Clear();int defending=1-value.Possession;var plan=Plan(defending);
        bool counterPress=(plan.DefensiveTransition=="Pressionar após perda"||plan.DefensiveTransition==null&&plan.Pressure=="Alta")&&value.Second-changedAt<72;
        int count=plan.Pressure=="Alta"?2:1;
        foreach(var p in Team(defending).Where(p=>p.Slot>0 && Role(p) is not ("Pressionar menos" or "Preservar energia")).OrderBy(p=>Distance(p.X,p.Z,value.BallX,value.BallZ)+(Fitness(p)<45?5:0)-(Role(p)=="Pressionar mais"?9:0)).Take(count))
            if(counterPress||Distance(p.X,p.Z,value.BallX,value.BallZ)<(plan.Pressure=="Baixa"?14:30))pressers.Add(p.Index);
    }
    public IReadOnlyList<FootballObservation> Visible(FootballObservation p)
    {
        var radius=24+FootballSkills.Get(p.Player,"Visão")*.38;var facingX=Math.Sin(p.Yaw);var facingZ=Math.Cos(p.Yaw);
        return context.Players.Where(x=>x.Active&&x.Index!=p.Index).Where(x=>{double dx=x.X-p.X,dz=x.Z-p.Z,d=Distance(x.X,x.Z,p.X,p.Z);return d<8||d<=radius&&(dx*facingX+dz*facingZ>=-d*.55||x.Team==p.Team&&d<32);}).ToArray();
    }
    public double Pressure(FootballObservation p)=>Math.Clamp(Visible(p).Where(x=>x.Team!=p.Team).Sum(x=>Math.Max(0,1-Distance(x.X,x.Z,p.X,p.Z)/7)),0,2)/2;
    public double PassingRisk(FootballObservation from,double x,double z,bool actual=false)
    {
        double length=Distance(from.X,from.Z,x,z);if(length<.1)return 1;
        double risk=0;var opponents=actual?Team(1-from.Team):Visible(from).Where(p=>p.Team!=from.Team);
        foreach(var d in opponents){double projection=((d.X-from.X)*(x-from.X)+(d.Z-from.Z)*(z-from.Z))/(length*length);if(projection<.04||projection>1.08)continue;double lane=FootballDecisions.PassingLaneDistance(d.X,d.Z,from.X,from.Z,x,z);double ballTime=length*Math.Clamp(projection,0,1)/19;double defenderTime=Math.Max(0,lane-.65)/(TopSpeed(d,true))+.10+(100-FootballSkills.Get(d.Player,"Antecipação"))*.003;double reach=Math.Clamp(.58+(ballTime-defenderTime)*.85,0,1);risk=Math.Max(risk,reach);}
        return risk;
    }
    (double X,double Z) Anchor(FootballObservation p)
    {
        if(Plan(p.Team).Positions?.TryGetValue(p.Player.Id,out var custom)==true)return(custom.X*52.5*Direction(p.Team),custom.Z*34*Direction(p.Team));
        var active=Team(p.Team).OrderBy(a=>a.Slot).ToList();var points=Tactics.AdvancedSlots(context.Teams[p.Team].Formation,(int)Direction(p.Team),active.Count);int i=active.FindIndex(a=>a.Index==p.Index);return points[Math.Clamp(i,0,points.Count-1)];
    }
    string Role(FootballObservation p){string role=Plan(p.Team).Roles?.GetValueOrDefault(p.Player.Id,"Livre")??"Livre";if(role!="Livre")return role;if(p.Player.Position=="ATA"&&Math.Abs(Anchor(p).Z)<8){if(Plan(p.Team).Style=="Direto")return "Pivô";if(Plan(p.Team).Style=="Posse"&&FootballSkills.Get(p.Player,"Visão")>=78)return "Falso nove";}return role;}
    public FootballTarget Target(FootballObservation p)
    {
        double dir=Direction(p.Team);var plan=Plan(p.Team);var anchor=Anchor(p);double x=anchor.X,z=anchor.Z;bool hasBall=p.Team==context.Possession;
        if(p.Slot==0)return KeeperTarget(p);
        if(p.Index==context.Carrier&&context.Flight==null){if(carrying.TryGetValue(p.Index,out var target))return target;return OpenCarry(p);}
        if(context.Flight?.Recipient==p.Index)return new(context.Flight.TargetX,context.Flight.TargetZ,true,"Receber");
        double width=plan.Width=="Aberta"?1.18:plan.Width=="Fechada"?.75:1;
        z=z*width+context.BallZ*.16;
        string role=Role(p);
        if(hasBall){
            double mentality=Tactics.MentalityShift(context.Teams[p.Team].Mentality);
            x+=context.BallX*.30+dir*(4+mentality);bool transition=plan.Style=="Contra-ataque"&&context.Second-changedAt<120;
            if(transition)x+=dir*(p.Player.Position=="ATA"?9:4);
            var runners=Team(p.Team).Where(a=>a.Slot>0&&a.Index!=context.Carrier).OrderByDescending(a=>a.X*dir+(a.Player.Position=="ATA"?7:0)+FootballSkills.Get(a.Player,"Posicionamento")*.06).Take(2).Select(a=>a.Index).ToHashSet();
            if((runners.Contains(p.Index)||role=="Infiltrar")&&role is not("Apoio" or "Cobertura" or "Pivô" or "Falso nove")){x=Math.Max(x*dir,Owner.X*dir+7)*dir;z+=Math.Sign(z-Owner.Z)*2.5;}
            bool overlap=(plan.Overlap||role=="Ultrapassar")&&(p.Player.Position=="LAT"||context.Teams[p.Team].Formation=="3-5-2"&&Math.Abs(anchor.Z)>20)&&Math.Sign(anchor.Z)==Math.Sign(context.BallZ)&&Math.Abs(context.BallZ)>6;
            if(overlap){x=dir*Math.Max(x*dir,context.BallX*dir+6);z=Math.Sign(anchor.Z)*29;}
            if(p.Player.Position=="LAT"&&!overlap){x-=dir*5;}
            if(plan.Overlap&&p.Player.Position=="ATA"&&Math.Abs(anchor.Z)>12&&Math.Sign(anchor.Z)==Math.Sign(context.BallZ))z*=.55;
            if(role=="Falso nove"||role=="Pivô"){x=dir*Math.Clamp(context.BallX*dir+6,-10,30);z=context.BallZ*.3;}
            if(role is "Cobertura" or "Marcar")x-=dir*5;
            if(role=="Apoio"||Distance(p.X,p.Z,Owner.X,Owner.Z)>24&&p.Player.Position=="MEI"){x=x*.4+(Owner.X-dir*6)*.6;z=z*.4+(Owner.Z+(p.Slot%2==0?8:-8))*.6;}
            if(arrivals.TryGetValue(context.Carrier,out var arrival)&&context.Second-arrival.At<60&&arrival.Sender==p.Index){x=Owner.X+dir*8;z=Owner.Z+(anchor.Z>=Owner.Z?6:-6);}
            double line=Team(1-p.Team).Select(d=>d.X*dir).OrderDescending().Skip(1).DefaultIfEmpty(50).First();
            x=dir*Math.Min(x*dir,Math.Max(line,context.BallX*dir)-.8);
        }else{
            double block=plan.Pressure=="Alta"?7:plan.Pressure=="Baixa"?-6:0;
            x=anchor.X*.70+context.BallX*.27+dir*(-4+block);z=anchor.Z*.80+context.BallZ*.23;
            if(pressers.Contains(p.Index)){int rank=pressers.OrderBy(i=>Distance(context.Players[i].X,context.Players[i].Z,context.BallX,context.BallZ)).ToList().IndexOf(p.Index);x=context.BallX-dir*(rank==0?1.1:3.5);z=context.BallZ+(rank==0?0:context.BallZ>0?-3:3);return Bounded(p,x,z,true,rank==0?"Pressão":"Cobertura da pressão");}
            var danger=Visible(p).Where(a=>a.Team!=p.Team&&a.Slot>0&&a.Index!=context.Carrier).OrderBy(a=>Distance(a.X,a.Z,p.X,p.Z)-Math.Max(0,-a.X*dir)*.1).FirstOrDefault();
            if(danger!=null&&Distance(danger.X,danger.Z,p.X,p.Z)<16){double cover=role is "Cobertura" or "Marcar por zona"?.25:role is "Marcar" or "Marcar jogador"?.75:.5;x=x*(1-cover)+(danger.X-dir*1.8)*cover;z=z*(1-cover)+danger.Z*cover;}
            if(p.Player.Position=="MEI"){x=x*.8+(Owner.X-dir*7)*.2;z=z*.8+Owner.Z*.2;}
            if(plan.OffsideTrap&&p.Player.Position is "ZAG" or "LAT"&&context.BallX*dir>-20)x=dir*Math.Max(x*dir,-22+context.BallX*dir*.15);
        }
        x += role switch { "Subir mais" => dir*6, "Recuar" => -dir*6, "Atacar profundidade" when hasBall => dir*8, _ => 0 };
        if (role == "Manter posição") { x = anchor.X; z = anchor.Z; }
        if (hasBall && role == "Cortar para dentro") z *= .45;
        if (hasBall && role == "Atacar a área") { x = x*.5 + dir*39*.5; z *= .55; }
        if (hasBall && role == "Aproximar para receber") { x = x*.5 + Owner.X*.5; z = z*.5 + Owner.Z*.5; }
        if (hasBall && role == "Movimentar livremente") { x += (Owner.X-anchor.X)*.16; z += (Owner.Z-anchor.Z)*.35; }
        if (hasBall && role == "Fazer ultrapassagens") { x += dir*7; z = Math.Sign(anchor.Z)*Math.Max(18,Math.Abs(z)); }
        // Space between team-mates is a target preference; contact resolution
        // remains exclusively in the existing physics step.
        foreach(var other in Team(p.Team).Where(a=>a.Index!=p.Index&&a.Slot>0)){double d=Distance(x,z,other.X,other.Z);if(d<4){z+=(z>=other.Z?1:-1)*(4-d)*.65;x+=(x>=other.X?1:-1)*(4-d)*.25;}}
        return Bounded(p,x,z,false,hasBall?"Apoio e ocupação":"Recomposição");
    }
    FootballTarget Bounded(FootballObservation p,double x,double z,bool sprint,string role)=>new(Math.Clamp(x,-50.5,50.5),Math.Clamp(z,-31.5,31.5),sprint,role);
    FootballTarget OpenCarry(FootballObservation p)
    {
        double dir=Direction(p.Team),best=-100;FootballTarget target=new(p.X+dir*6,p.Z,true,"Condução");
        foreach(double lateral in new[]{-7.0,0,7}){double x=Math.Clamp(p.X+dir*8,-50,50),z=Math.Clamp(p.Z+lateral,-31,31);double space=Visible(p).Where(d=>d.Team!=p.Team).Select(d=>Distance(x,z,d.X,d.Z)).DefaultIfEmpty(18).Min();double approach=Math.Max(0,Math.Abs(p.Z)-Math.Abs(z))*(p.X*dir>30?.32:.04);double endLinePenalty=Math.Max(0,x*dir-45)*.30;double score=space-Math.Abs(z)*.06+approach-endLinePenalty+(Plan(p.Team).AttackSide=="Esquerda"&&z*dir<0||Plan(p.Team).AttackSide=="Direita"&&z*dir>0?2:0);if(score>best){best=score;target=new(x,z,true,"Condução em espaço livre");}}
        return target;
    }
    FootballTarget KeeperTarget(FootballObservation p)
    {
        double dir=Direction(p.Team),distance=context.BallX*dir+52;double advance=context.Possession==p.Team&&(Plan(p.Team).BuildShort||Role(p)=="Apoio")?Role(p)=="Apoio"?7:5:Math.Clamp((35-distance)*.10,.5,3.8);double x=dir*(-51+advance),z=Math.Clamp(context.BallZ*.18,-3.2,3.2);
        double skill=FootballSkills.Get(p.Player,"Goleiro"),reaction=.15+(100-skill)*.003+(100-Fitness(p))*.002;
        if(context.Flight is {Shot:true} shot&&shot.Team!=p.Team&&(context.Second-shot.Start)/12.0>=reaction&&Math.Abs(velocityBallX)>.1){double t=(dir*-51.5-context.BallX)/velocityBallX;if(t>0&&t<2)z=Math.Clamp(context.BallZ+velocityBallZ*t,-4.3,4.3);}
        if(context.Flight is {Shot:false} cross&&cross.Team!=p.Team&&cross.Delivery=="cross"&&Distance(p.X,p.Z,context.BallX,context.BallZ)<8&&context.BallY<2.5){double opponentTime=Team(1-p.Team).Select(a=>Distance(a.X,a.Z,context.BallX,context.BallZ)/TopSpeed(a,true)).DefaultIfEmpty(5).Min();if(Distance(p.X,p.Z,context.BallX,context.BallZ)/TopSpeed(p,true)+reaction<opponentTime){x=context.BallX;z=context.BallZ;}}
        return new(Math.Clamp(x,-51.5,51.5),Math.Clamp(z,-10,10),advance>4,"Fechar ângulo");
    }
    public List<FootballChoice> Choices(int index)
    {
        var p=context.Players[index];double dir=Direction(p.Team),pressure=Pressure(p);var plan=Plan(p.Team);var choices=new List<FootballChoice>();var visible=Visible(p);double vision=FootballSkills.Normal(p.Player,"Visão");
        foreach(var mate in visible.Where(a=>a.Team==p.Team&&a.Index!=index)){
            double distance=Distance(p.X,p.Z,mate.X,mate.Z);if(distance<3||distance>15+vision*48)continue;
            bool backpass=mate.Slot==0;double openness=visible.Where(d=>d.Team!=p.Team).Select(d=>Distance(d.X,d.Z,mate.X,mate.Z)).DefaultIfEmpty(20).Min();
            var target=Target(mate);double lead=mate.Slot==0?0:Math.Min(1.2,distance/19);double tx=mate.X+Math.Clamp(target.X-mate.X,-TopSpeed(mate,true)*lead,TopSpeed(mate,true)*lead),tz=mate.Z+Math.Clamp(target.Z-mate.Z,-4,4);
            bool through=tx*dir>p.X*dir+10&&mate.Slot>0&&mate.Player.Position is "ATA" or "MEI";
            bool crossing=Math.Abs(p.Z)>17&&p.X*dir>6&&mate.X*dir>20&&Math.Abs(mate.Z)<18;
            bool changeSide=p.Z*mate.Z<-40&&distance>22;bool returnPass=arrivals.TryGetValue(index,out var arrival)&&context.Second-arrival.At<48&&arrival.Sender==mate.Index;
            bool highCross=crossing&&(Math.Abs(p.Z)>24||mate.X*dir<30);
            string kind=crossing?highCross?"cross_high":"cross_low":returnPass?"give_go":changeSide?"switch":through?"through":plan.Style=="Direto"&&distance>25?"long_ball":arrivals.TryGetValue(index,out var arrived)&&context.Second-arrived.At<8&&FootballSkills.Get(p.Player,"Primeiro toque")>=75?"one_touch":"pass";
            double risk=PassingRisk(p,tx,tz),progress=Math.Clamp((tx-p.X)*dir/25,-.6,1);
            double score=.24+(1-risk)*.22+Math.Min(1,openness/8)*.10+progress*.18-Math.Max(0,distance-22)*.003+(returnPass?.12:0)+(through?vision*.07:0);
            score+=FootballSkills.Normal(p.Player,"Trabalho em equipe")*.04+(through||changeSide?FootballSkills.Normal(p.Player,"Criatividade")*.04:0);
            if(plan.Style=="Posse")score+=distance<20?.12:-.12;if(plan.Style=="Direto")score+=distance>22?.1:-.06;
            if(plan.Style=="Contra-ataque"&&context.Second-changedAt<120)score+=progress*.14;
            if(plan.AttackSide!="Ambos")score+=(plan.AttackSide=="Esquerda"?mate.Z*dir<0:mate.Z*dir>0)?.07:-.04;
            score += Role(p) switch {
                "Passar curto" => distance<20?.14:-.12, "Arriscar passes" => progress*.14+risk*.07,
                "Cruzar mais" when crossing => .19, _ => 0
            };
            if(crossing)score+=.05;if(backpass)score+=pressure>.6?.08:-.17;
            if(p.Slot==0)score+=plan.BuildShort?(distance<25?.2:-.18):(distance>25?.16:-.10);
            bool offside=FootballDecisions.Offside(mate.X,mate.Yaw,p.X,dir,Team(1-p.Team).Select(d=>(d.X,d.Yaw)));if(offside)score-=.65;
            choices.Add(new(kind,mate.Index,Math.Clamp(tx,-51,51),Math.Clamp(tz,-32,32),score,risk,kind switch{"give_go"=>"Tabelinha","through"=>"Passe em profundidade","switch"=>"Inversão de jogo","cross_high"=>p.X*dir<24?"Cruzamento antecipado":"Cruzamento alto","cross_low"=>"Cruzamento rasteiro","one_touch"=>"Passe de primeira","long_ball"=>"Bola longa",_=>Role(mate)=="Pivô"?"Passe no pivô":Role(mate)=="Falso nove"?"Falso nove entre linhas":"Triangulação e apoio"}));
        }
        var carry=OpenCarry(p);double clearance=visible.Where(d=>d.Team!=p.Team).Select(d=>Distance(d.X,d.Z,carry.X,carry.Z)).DefaultIfEmpty(18).Min();
        double goalDistance=Distance(p.X,p.Z,dir*52,0);
        double shootingLane=PassingRisk(p,dir*52,0);
        double clearChance=Math.Clamp((24-goalDistance)/16,0,1)*(1-shootingLane)*(1-pressure);
        double endLineRisk=Math.Clamp((p.X*dir-40)/10,0,1);
        choices.Add(new("dribble",index,carry.X,carry.Z,.40+Math.Min(1,clearance/9)*.22+FootballSkills.Normal(p.Player,"Drible")*.09-pressure*.20-clearChance*.30-endLineRisk*.18+(Role(p)=="Driblar mais"?.15:Role(p) is "Driblar menos" or "Preservar energia"?-.15:0),Pattern:context.Second-changedAt<120&&plan.Style=="Contra-ataque"?"Contra-ataque":"Condução em espaço livre"));
        choices.Add(new("shield",index,p.X,p.Z,.16+pressure*.53+FootballSkills.Normal(p.Player,"Força")*.10,Pattern:"Proteção da bola"));
        if(p.Slot>0){double quality=ShotQuality(p);choices.Add(new("shot",-1,dir*53,0,.16+quality*.94+clearChance*.85+(plan.ShootMore?.18:0)+(pressure<.2&&quality>.13?.10:0)+Math.Max(0,Tactics.MentalityShift(context.Teams[p.Team].Mentality))*.0075+(Role(p)=="Chutar mais"?.17:Role(p)=="Chutar menos"?-.19:0)-pressure*.06,Pattern:p.X*dir<30?"Finalização de fora da área":"Finalização no espaço"));}
        return choices;
    }
    public FootballChoice Decide(int index)
    {
        var p=context.Players[index];var candidates=Choices(index);double noise=(1-FootballSkills.Normal(p.Player,"Decisões"))*.22+(1-FootballSkills.Normal(p.Player,"Concentração"))*.07+(100-Fitness(p))*.0006;
        double jitter(FootballChoice c){uint h=unchecked((uint)(seed+p.Player.Id*7919+context.Second*104729+c.Recipient*31+c.Kind.Sum(x=>(int)x)*17));h^=h>>16;h*=0x7feb352d;h^=h>>15;return ((h%10001)/10000.0-.5)*noise;}
        var best=candidates.OrderByDescending(c=>c.Utility+jitter(c)).First();if(best.Kind=="dribble")carrying[index]=new(best.X,best.Z,true,best.Pattern);else if(best.Kind=="shield")carrying[index]=new(p.X,p.Z,false,"Proteção");else carrying.Remove(index);return best;
    }
    public double PassAccuracy(FootballObservation p,FootballChoice choice)
    {
        double risk=PassingRisk(p,choice.X,choice.Z,true),distance=Distance(p.X,p.Z,choice.X,choice.Z),lateral=(choice.X-p.X)*Math.Cos(p.Yaw)-(choice.Z-p.Z)*Math.Sin(p.Yaw);double foot=p.Player.PreferredFoot=="Esquerdo"&&lateral>5||p.Player.PreferredFoot=="Direito"&&lateral<-5?.025:0;
        return Math.Clamp(.71+FootballSkills.Get(p.Player,"Passe")*.0024+FootballSkills.Get(p.Player,"Técnica")*.0004-risk*.36-Pressure(p)*.10-Math.Max(0,distance-16)*.002-foot-(100-Fitness(p))*.001,.35,.97);
    }
    public int DecisionDelay(int index){var p=context.Players[index];return (int)Math.Clamp(14-FootballSkills.Get(p.Player,"Primeiro toque")*.10+Pressure(p)*4+(100-Fitness(p))*.03+(Plan(p.Team).Tempo=="Alto"?-3:Plan(p.Team).Tempo=="Baixo"?3:0),2,16);}
    public void PassArrived(int sender,int receiver,int second){arrivals[receiver]=(sender,second);carrying.Remove(sender);}
    public double ShotQuality(FootballObservation p)
    {
        double dir=Direction(p.Team),distance=Distance(p.X,p.Z,dir*52,0),angle=Math.Atan2(7.32*Math.Max(1,52-p.X*dir),Math.Max(1,Sqr(52-p.X*dir)+Sqr(p.Z)-13.4));
        return Math.Clamp(Math.Exp(-distance/21)*Math.Clamp(angle/1.0,.05,1)*(1.1+FootballSkills.Normal(p.Player,"Finalização")*.35),.005,.85);
    }
    public FootballShot Shoot(int index,bool penalty,bool header,Random random)
    {
        var p=context.Players[index];var keeper=context.Players.First(a=>a.Active&&a.Team!=p.Team&&a.Slot==0);double dir=Direction(p.Team),quality=ShotQuality(p),fin=FootballSkills.Get(p.Player,"Finalização"),gk=FootballSkills.Get(keeper.Player,"Goleiro");
        double reaction=.15+(100-gk)*.003+(100-Fitness(keeper))*.002;
        double aim=(keeper.Z>0?-1:keeper.Z<0?1:random.NextDouble()<.5?-1:1)*(1.2+random.NextDouble()*2.1);
        aim+=(random.NextDouble()-random.NextDouble())*(.5+(100-fin)*.04+Pressure(p)*.6);
        double accuracy=Math.Clamp(.46+fin*.0035-(penalty?0:Pressure(p)*.15+Distance(p.X,p.Z,dir*52,0)*.004),.25,.94);
        bool onTarget=Math.Abs(aim)<3.55&&random.NextDouble()<accuracy;
        double flight=Distance(p.X,p.Z,dir*52,aim)/(header?18:28),reach=1.25+Math.Max(0,flight-reaction)*(3.7+FootballSkills.Get(keeper.Player,"Agilidade")*.025);
        bool save=onTarget&&Math.Abs(aim-keeper.Z)<=reach&&random.NextDouble()<Math.Clamp(.50+gk*.003-FootballSkills.Get(p.Player,"Compostura")*.001,.35,.87);
        string result=!onTarget?"miss":save?"save":"goal";
        return new(result,dir*(result=="goal"?54:result=="save"?Math.Abs(keeper.X)-.2:53),!onTarget?Math.Sign(aim)*(4.5+random.NextDouble()*3):aim,quality,reaction);
    }
    public bool Announce(FootballChoice choice,int player,int second){if(choice.Kind is "pass" or "dribble"||lastPattern.GetValueOrDefault(player,-100)<second-48){lastPattern[player]=second;return choice.Kind is not("pass" or "dribble");}return false;}
    public CoachAdjustment? Coach(int team,int second,IReadOnlyList<MatchPlayer> bench,int used)
    {
        if(!context.Teams[team].ComputerControlled||second<900||second-lastCoach[team]<600)return null;
        lastCoach[team]=second;var old=context.Teams[team];var plan=Plan(team);int diff=team==0?context.HomeGoals-context.AwayGoals:context.AwayGoals-context.HomeGoals;
        string formation=old.Formation,mentality=old.Mentality,reason="";
        if(Team(team).Count()<11){mentality="Defensiva";plan=plan with{Pressure="Baixa",Width="Fechada",Overlap=false};reason="Reorganização após expulsão.";}
        else if(second>=4200&&diff>0){mentality="Defensiva";plan=plan with{Style="Contra-ataque",Pressure="Baixa",Width="Fechada",Overlap=false};reason="Proteger a vantagem e guardar saídas para o contra-ataque.";}
        else if(second>=3300&&diff<0){mentality="Ofensiva";formation="4-2-3-1";plan=plan with{Pressure="Alta",Width="Aberta",Overlap=true,ShootMore=second>4500};reason="Buscar o empate com pressão e apoio dos laterais.";}
        else{
            double left=Team(1-team).Where(p=>p.Z*Direction(team)<0&&p.Slot>0).Select(Fitness).DefaultIfEmpty(100).Average(),right=Team(1-team).Where(p=>p.Z*Direction(team)>=0&&p.Slot>0).Select(Fitness).DefaultIfEmpty(100).Average();
            if(Math.Abs(left-right)>5){plan=plan with{AttackSide=left<right?"Esquerda":"Direita"};reason="Explorar o corredor com adversários mais cansados.";}
            if(context.Teams[1-team].Plan?.Pressure=="Alta"){plan=plan with{Style="Contra-ataque",BuildShort=false};reason="Superar a pressão adversária com saídas em profundidade.";}
        }
        int outgoing=0,incoming=0;
        if(second>=3300&&used<5&&old.AutoSubstitute){var tired=Team(team).Where(p=>p.Slot>0).OrderBy(Fitness).FirstOrDefault(p=>Fitness(p)<68&&bench.Any(b=>b.Position==p.Player.Position));if(tired!=null){var fresh=bench.Where(b=>b.Position==tired.Player.Position).MaxBy(p=>p.Fitness*.6+p.Rating*.4)!;outgoing=tired.Player.Id;incoming=fresh.Id;reason+=(reason==""?"":" ")+"Renovar o setor desgastado.";}}
        if(formation==old.Formation&&mentality==old.Mentality&&plan==old.Plan&&outgoing==0)return null;
        return new(formation,mentality,plan,reason,outgoing,incoming);
    }
}

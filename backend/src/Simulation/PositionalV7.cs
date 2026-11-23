using Checkpoint = Brasileirao.PositionalV5.Checkpoint;
namespace Brasileirao;

// O renderer nunca decide posições, posse, gols ou placar. O mesmo snapshot e seed
// geram a partida resumida e a reprodução. Frames são materializados apenas sob demanda.
public static class PositionalV7
{
    public const string Version = "positional-v7";
    public const int Duration = 90 * 60;
    // V1–V6 remain frozen. V7 records coach commands and explicit halftime.
    public const int TimeScale = 12;
    const int Tick = 2;
    static int FlightTicks(double distance, double metresPerSecond) => Math.Clamp((int)Math.Ceiling(distance / metresPerSecond * TimeScale / Tick) * Tick, 6, 48);
    sealed class Agent(MatchPlayer player, int team, int slot, double x, double z)
    {
        public MatchPlayer Player = player;
        public int Team = team, Slot = slot;
        public int Index => Team * 11 + Slot;
        public double VelocityX, VelocityZ;
        public double X = x, Z = z, Yaw = team == 0 ? Math.PI / 2 : -Math.PI / 2;
        public string Action = "idle";
        public int ActionUntil;
        public int ActionSecond;
        public bool Active = true;
    }
    sealed record Flight(double X, double Z, double TargetX, double TargetZ, int Start, int End, int Recipient, string Result, int Shooter, int Team, MatchPlayer ShooterPlayer, string Delivery = "ground", double StartY = .24, double EndY = .24, bool OffsideRecipient = false, bool Reviewed = false, string Infringement = "");
    static (int HalfEnd,int Duration) EnsurePenaltyTime(int second,int halfEnd,int duration)
    {
        if(second<halfEnd && second+120>halfEnd){var extra=second+120-halfEnd;return (halfEnd+extra,duration+extra);}
        return (halfEnd,Math.Max(duration,second>=halfEnd+2600?second+120:duration));
    }
    static double ClampX(double x) => Math.Clamp(x, -51.5, 51.5);
    static double ClampZ(double z) => Math.Clamp(z, -32.5, 32.5);
    static double Round(double n) => Math.Round(n, 2);
    static double Rating(MatchPlayer p) => p.Rating * (.65 + .35 * p.Fitness / 100.0);

    public static MatchReplay Run(MatchSetup setup, int seed, bool captureFrames) => Stream(setup, seed, captureFrames).Last().Replay;
    public static IEnumerable<Checkpoint> Stream(MatchSetup setup, int seed, bool captureFrames = true,FootballBrain? intelligence=null,bool delayedKeeper=false,bool physicalShots=false,bool continuousContacts=false)
    {
        if (setup.EngineVersion != Version) throw new GameRuleException("Versão do motor não suportada para esta reprodução.");
        if (setup.Home.Players.Count != 11 || setup.Away.Players.Count != 11) throw new GameRuleException("Uma partida exige duas equipes de onze jogadores.");
        Sports.ValidateTeam(setup.Home); Sports.ValidateTeam(setup.Away);
        int halfEnd = 2700, duration = Duration; bool penaltyKick = false, offsideExempt = false;
        var rules = new Random(seed ^ 0x73CA);
        var rng = new Random(seed);
        var aerial = new Random(seed ^ 0x6F17); var duels = new Random(seed ^ 0xD3E1);
        int lastDuel = -80; var appliedCommands = new HashSet<int>();
        int throwCarrier = -1, throwRelease = -1;
        (int Until, int Team, double X, double Z, int Victim)? advantage = null;
        var discipline = new Random(seed ^ 0x51A7); var medical = new Random(seed ^ 0x371B);
        MatchTeam[] teams = [setup.Home, setup.Away];
        List<(double X,double Z)> Slots(string formation,int side)=>intelligence==null?Tactics.Slots(formation,side):Tactics.AdvancedSlots(formation,side);
        var slots = new[] { Slots(teams[0].Formation, 1), Slots(teams[1].Formation, -1) };
        var agents = teams.SelectMany((t, team) => t.Players.Select((p, slot) => new Agent(p, team, slot, slots[team][slot].X, slots[team][slot].Z))).ToArray();
        var unused = teams.Select(t => (t.Bench ?? []).ToList()).ToArray();
        int[] substitutions = [0, 0];
        HashSet<int>[] substitutionStops = [[], []]; var donePlans = new HashSet<(int, int)>(); var bookings = new Dictionary<int, int>(); var secondsPlayed = new Dictionary<int, int>();
        bool[] injuryOccurred = [false, false];
        List<ReplayFrame> frames = []; List<MatchEvent> events = [];
        int possession = 0, carrier = 6, decisionAt = 12, kickoffUntil = 0, homeGoals = 0, awayGoals = 0, lastShot = -150;
        double ballX = agents[carrier].X, ballZ = agents[carrier].Z, ballY = .24;
        Flight? flight = null;
        BallPhysics? physicalBall = null;
        BallContact? physicalContact = null;
        int contactSequence = 0;
        BallContact RecordContact(int playerId,int team,string type,int second,double x,double y,double z,string origin) => new(playerId,team,type,second,x,y,z,origin,++contactSequence);
        BallRestart? pendingPhysicalRestart = null;
        Position3? physicalRestartSpot = null;
        var touchingPlayers=new HashSet<int>();
        var physicalOffside = new HashSet<int>();
        void OpponentContact(OpponentBallPlay play)
        {if(!new OffsideSnapshot(true,false).AfterOpponentContact(play).PositionedOffside)physicalOffside.Clear();}
        var jumps=new Dictionary<int,int>();
        double HeadHeight(Agent player,int at)=>AerialPlay.HeadHeight(jumps.TryGetValue(player.Index,out var start)?(at-start)/(double)TimeScale:-1,FootballSkills.Get(player.Player,"Força"),FootballSkills.Get(player.Player,"Agilidade"));
        double ContactRadius(Agent a)=>a.Slot==0 && a.Action is "dive_left" or "dive_right" or "dive_low" ? .85 : .42;
        FootballObservation ObserveAgent(Agent a)=>new(a.Index,a.Player,a.Team,a.Slot,a.X,a.Z,a.Yaw,a.VelocityX,a.VelocityZ,a.Active);
        FootballContext Observe(int second)=>new(second,halfEnd,duration,possession,carrier,ballX,ballY,ballZ,teams,agents.Select(a=>new FootballObservation(a.Index,a.Player,a.Team,a.Slot,a.X,a.Z,a.Yaw,a.VelocityX,a.VelocityZ,a.Active)).ToArray(),flight==null?null:new(flight.Recipient,flight.Team,flight.TargetX,flight.TargetZ,flight.Start,flight.End,flight.Delivery,flight.Result is "goal" or "save" or "miss"),homeGoals,awayGoals);
        int headerCarrier = -1; bool forceCapture = false;
        void Pose(Agent a, string action, int second, int until) { a.ActionSecond = second; a.Action = action; a.ActionUntil = until; forceCapture = true; }

        void Event(int second, string type, int team, int player, string text, int? related = null, int injuryDays = 0) => events.Add(new(Math.Max(1, (int)Math.Ceiling(second / 60.0)), type, teams[team].Id, player, text, second, related, injuryDays));
        void Capture(int second)
        {
            if (!captureFrames || frames.Count > 0 && frames[^1].Second == second) return;
            frames.Add(new(second, agents.Select(a => new PlayerPose(a.Player.Id, a.Team, Round(a.X), Round(a.Z), Round(a.Yaw), a.Action, a.Active, a.Player.Name, a.Player.Position, a.ActionSecond, intelligence == null ? null : second == 0 ? a.Player.Fitness : (int)Math.Round(intelligence.Fitness(ObserveAgent(a))))).ToList(), new(Round(ballX), Round(ballY), Round(ballZ)), intelligence == null ? null : possession, physicalShots ? physicalContact : null, physicalShots && physicalBall != null ? new Position3(physicalBall.VX, physicalBall.VY, physicalBall.VZ) : null, physicalShots ? new ReplayDiagnostics(second >= duration ? "finished" : second == halfEnd ? "halftime" : pendingPhysicalRestart != null ? "restart_preparation" : second < kickoffUntil ? "stoppage" : "in_play", pendingPhysicalRestart?.ToString(), events.Count > 0 ? events.Count - 1 : null) : null));
        }
        Agent Nearest(int team, double x, double z) => agents.Where(a => a.Active && a.Team == team && a.Slot > 0).MinBy(a => Math.Pow(a.X - x, 2) + Math.Pow(a.Z - z, 2))!;
        void EnsureCarrier() { if (!agents[carrier].Active) { carrier = Nearest(possession, ballX, ballZ).Index; decisionAt += 4; } }
        bool Replace(Agent outgoing, MatchPlayer incoming, int second, string reason, bool halfTimeChange = false)
        {
            if (!halfTimeChange && second != halfEnd && !substitutionStops[outgoing.Team].Contains(second) && substitutionStops[outgoing.Team].Count >= 3) return false;
            if (substitutions[outgoing.Team] >= 5 || !outgoing.Active || !unused[outgoing.Team].Any(p => p.Id == incoming.Id)) return false;
            var original = outgoing.Player;
            if(intelligence!=null&&teams[outgoing.Team].Plan is {Positions:not null} customPlan){var positions=new Dictionary<int,TacticalPoint>(customPlan.Positions);var roles=new Dictionary<int,string>(customPlan.Roles??[]);if(positions.TryGetValue(original.Id,out var point)){positions.Remove(original.Id);positions[incoming.Id]=point;}if(roles.TryGetValue(original.Id,out var role)){roles.Remove(original.Id);roles[incoming.Id]=role;}teams[outgoing.Team]=teams[outgoing.Team] with{Plan=customPlan with{Positions=positions,Roles=roles}};}
            if (outgoing.Slot == 0 && incoming.Position != "GOL") incoming = incoming with { NaturalPosition = incoming.Position, Position = "GOL", Rating = Math.Max(20, incoming.Rating / 2) };
            if (!halfTimeChange && second != halfEnd) substitutionStops[outgoing.Team].Add(second);
            unused[outgoing.Team].RemoveAll(p => p.Id == incoming.Id); substitutions[outgoing.Team]++; outgoing.Player = incoming; outgoing.Action = "enter"; outgoing.ActionUntil = second + 8;
            Event(second, "substitution", outgoing.Team, original.Id, $"Sai {original.Name}; entra {incoming.Name}. {reason}", incoming.Id);
            return true;
        }
        MatchPlayer? Reserve(Agent outgoing) => unused[outgoing.Team].Where(p => outgoing.Slot == 0 ? p.Position == "GOL" : p.Position != "GOL")
            .OrderByDescending(p => (p.Position == outgoing.Player.Position ? 30 : 0) + Rating(p)).FirstOrDefault();
        void Remove(Agent agent, int second)
        {
            agent.Active = false; agent.Action = "leave"; agent.ActionUntil = Duration;
            if (agent.Slot == 0)
            {
                var emergency = agents.Where(a => a.Active && a.Team == agent.Team && a.Slot > 0).MinBy(a => a.Player.Rating)!;
                agent.Player = emergency.Player with { NaturalPosition = emergency.Player.Position, Position = "GOL", Rating = Math.Max(20, emergency.Player.Rating / 2) };
                agent.Active = true; emergency.Active = false; emergency.Action = "leave";
                Event(second, "emergency_keeper", agent.Team, agent.Player.Id, $"{agent.Player.Name} passa a atuar como goleiro improvisado.");
            }
            EnsureCarrier();
        }
        Capture(0);
        for (int second = Tick; second <= duration; second += Tick)
        {
            // Suspend BEFORE the next step: no decision or random draw from the future is evaluated.
            yield return new(second - Tick, false, new(Version, duration, setup, events, frames, homeGoals, awayGoals));
            forceCapture = false;
            foreach (var a in agents.Where(a => a.Active)) secondsPlayed[a.Player.Id] = secondsPlayed.GetValueOrDefault(a.Player.Id) + Tick;
            if (second == 2700)
            {
                int added = MatchRules.AddedTime(events, 0, second); halfEnd += added; duration += added;
                Event(second, "added_time", 0, 0, $"Acréscimos no primeiro tempo: {added / 60} minuto(s).");
            }
            if (second == halfEnd + 2700)
            {
                int added = MatchRules.AddedTime(events, halfEnd, second); duration += added;
                Event(second, "added_time", 0, 0, $"Acréscimos no segundo tempo: {added / 60} minuto(s).");
            }
            if (second == halfEnd)
            {
                Event(second, "halftime", 0, 0, "Intervalo. As equipes se recolhem ao vestiário.");
                Capture(second);
                yield return new(second, false, new(Version, duration, setup, events, frames, homeGoals, awayGoals));
                slots = [Slots(teams[0].Formation, -1), Slots(teams[1].Formation, 1)];
                foreach (var a in agents) { a.X = -a.X; a.Z = -a.Z; a.Yaw += Math.PI; a.VelocityX = a.VelocityZ = 0; }
                flight=null; if(physicalShots)physicalOffside.Clear();
                        headerCarrier = -1; offsideExempt = false; penaltyKick = false; possession = 1; carrier = 17; EnsureCarrier(); ballX = ballZ = 0; ballY = .24; kickoffUntil = second + 24; decisionAt = kickoffUntil;
                if (physicalShots) { physicalBall=null; pendingPhysicalRestart=null; physicalRestartSpot=null; touchingPlayers.Clear(); }
                Event(second, "second_half", 0, 0, "Segundo tempo: as equipes trocam de lado.");
            }
            for(int commandIndex=0; commandIndex<(setup.Instructions?.Count ?? 0); commandIndex++)
            {
                var command=setup.Instructions![commandIndex];
                if(command.Second>second || appliedCommands.Contains(commandIndex))continue;
                var team=Array.FindIndex(teams,t=>t.Id==command.Team);if(team<0)throw new GameRuleException("Equipe inválida no comando da partida.");
                if(command.Type=="substitution" && flight!=null)continue;
                appliedCommands.Add(commandIndex);
                if(command.Type=="tactics")
                {
                    teams[team]=teams[team] with {Formation=command.Formation,Mentality=command.Mentality,Plan=command.Plan??teams[team].Plan};
                    slots[team]=Slots(command.Formation,(team==0?1:-1)*(second>=halfEnd?-1:1));
                    Event(second,"tactics_change",team,0,$"Mudança tática: {command.Formation}, mentalidade {command.Mentality}.");
                }
                else if(command.Type=="substitution")
                {
                    var outgoing=agents.FirstOrDefault(a=>a.Active&&a.Team==team&&a.Player.Id==command.OutPlayerId);
                    var incoming=unused[team].Find(p=>p.Id==command.InPlayerId);
                    if(outgoing==null||incoming==null||!Replace(outgoing,incoming,second,"Decisão do treinador.",command.AtHalfTime))
                        Event(second,"substitution_cancelled",team,command.OutPlayerId,"Troca não realizada: atleta indisponível ou limite atingido.",command.InPlayerId);
                    else {Pose(outgoing,"enter",second,second+12); if(!command.AtHalfTime)kickoffUntil=Math.Max(kickoffUntil,second+12);}
                }
            }
            if (flight != null)
            {
                if(delayedKeeper && intelligence!=null && (flight.Result is "goal" or "save" or "miss")){
                    var keeper=agents[(1-flight.Team)*11];
                    int reactionTicks=(int)Math.Ceiling((.15+(100-FootballSkills.Get(keeper.Player,"Goleiro"))*.003+(100-intelligence.Fitness(ObserveAgent(keeper)))*.002)*TimeScale/Tick)*Tick;
                    if(second==flight.Start+reactionTicks&&second<flight.End){double lateral=ballZ-keeper.Z;Pose(keeper,Math.Abs(lateral)<.5?"dive_low":lateral>0?"dive_left":"dive_right",second,flight.End+12);}
                }
                if (physicalBall != null)
                {
                    double previousX = physicalBall.X;
                    var previousBall=new Position3(physicalBall.X,physicalBall.Y,physicalBall.Z);
                    var interfering=agents.Where(a=>a.Active && physicalOffside.Contains(a.Index) && a.Action is "air_duel" or "header" or "challenge").OrderBy(a=>a.Index).FirstOrDefault(a=>
                        agents.Any(d=>d.Active && d.Team!=a.Team && Math.Pow(d.X-ballX,2)+Math.Pow(d.Z-ballZ,2)<2.25 && Math.Pow(d.X-a.X,2)+Math.Pow(d.Z-a.Z,2)<1)
                        && Math.Pow(a.X-ballX,2)+Math.Pow(a.Z-ballZ,2)<2.25 && ballY<2.5);
                    if(interfering!=null)
                    {
                        Event(second,"offside",interfering.Team,interfering.Player.Id,"Impedimento por disputar a bola e interferir no adversário, sem exigir toque.");
                        possession=1-interfering.Team;carrier=Nearest(possession,ballX,ballZ).Index;physicalBall=null;flight=null;physicalOffside.Clear();kickoffUntil=decisionAt=second+12;Capture(second);continue;
                    }
                    if(ballY>1.3)
                    {
                        var predicted=AerialPlay.Predict(physicalBall,.3);
                        var challengers=agents.Where(a=>a.Active && a.Slot>0 && !touchingPlayers.Contains(a.Index))
                            .GroupBy(a=>a.Team).Select(g=>g.OrderBy(a=>Math.Pow(a.X-predicted.X,2)+Math.Pow(a.Z-predicted.Z,2)).ThenBy(a=>a.Index).First());
                        foreach(var challenger in challengers)
                        {
                            double dx=predicted.X-challenger.X,dz=predicted.Z-challenger.Z,d=Math.Sqrt(dx*dx+dz*dz);
                            if(d>8)continue;
                            double step=Math.Min(d,(4+FootballSkills.Get(challenger.Player,"Antecipação")*.025)*Tick/TimeScale);
                            if(d>.01){challenger.X=Math.Clamp(challenger.X+dx/d*step,-52.5,52.5);challenger.Z=Math.Clamp(challenger.Z+dz/d*step,-34,34);}
                            if(d<2 && physicalBall.VY<2 && predicted.Y<2.6 && !agents.Any(a=>a.Team==challenger.Team && jumps.TryGetValue(a.Index,out var activeJump) && second-activeJump<10) && (!jumps.TryGetValue(challenger.Index,out var oldJump)||second-oldJump>18))
                            {jumps[challenger.Index]=second;Pose(challenger,"air_duel",second,second+12);Event(second,"aerial_challenge",challenger.Team,challenger.Player.Id,"Prepara salto para disputar a trajetória aérea.");}
                        }
                    }
                    if(flight.Delivery is "physical_pass" or "physical_cross")
                    {
                        double px=Math.Clamp(ballX+physicalBall.VX*.35,-52.5,52.5),pz=Math.Clamp(ballZ+physicalBall.VZ*.35,-34,34);
                        foreach(var runner in agents.Where(a=>a.Active && a.Index!=flight.Shooter && !touchingPlayers.Contains(a.Index))
                            .OrderBy(a=>Math.Pow(a.X-px,2)+Math.Pow(a.Z-pz,2)).ThenBy(a=>a.Index).Take(6))
                        {
                            double dx=px-runner.X,dz=pz-runner.Z,distance=Math.Sqrt(dx*dx+dz*dz);
                            double step=Math.Min(distance,(4+FootballSkills.Get(runner.Player,"Velocidade")*.035)*Tick/TimeScale);
                            if(distance>.01){runner.X+=dx/distance*step;runner.Z+=dz/distance*step;runner.Yaw=Math.Atan2(dx,dz);if(!jumps.TryGetValue(runner.Index,out var jumpStart)||second-jumpStart>=10)Pose(runner,"run",second,second+2);}
                        }
                    }
                    var reactingKeeper=agents[(1-flight.Team)*11];
                    if(!touchingPlayers.Contains(reactingKeeper.Index) && ballY>1.4 && flight.Delivery is "physical_cross" or "cross")
                    {
                        var arrival=AerialPlay.Predict(physicalBall,.45);
                        double ownSide=(reactingKeeper.Team==0?-1:1)*(second>=halfEnd?-1:1);
                        if(arrival.X*ownSide>36 && Math.Abs(arrival.Z)<18 && arrival.Y<3)
                        {
                            double dx=arrival.X-reactingKeeper.X,dz=arrival.Z-reactingKeeper.Z,d=Math.Sqrt(dx*dx+dz*dz);
                            double step=Math.Min(d,(3+FootballSkills.Get(reactingKeeper.Player,"Goleiro")*.035)*Tick/TimeScale);
                            if(d>.1){reactingKeeper.X=Math.Clamp(reactingKeeper.X+dx/d*step,ownSide>0?36:-52.5,ownSide>0?52.5:-36);reactingKeeper.Z=Math.Clamp(reactingKeeper.Z+dz/d*step,-18,18);Pose(reactingKeeper,"run",second,second+2);}
                        }
                    }
                    double reaction=.15+(100-FootballSkills.Get(reactingKeeper.Player,"Goleiro"))*.003;
                    if(!touchingPlayers.Contains(reactingKeeper.Index) && (second-flight.Start)/(double)TimeScale>=reaction && Math.Abs(physicalBall.VX)>.1)
                    {
                        double interceptTime=(reactingKeeper.X-physicalBall.X)/physicalBall.VX;
                        if(interceptTime is >=0 and <3)
                        {
                            double target=Math.Clamp(physicalBall.Z+physicalBall.VZ*interceptTime,-4.5,4.5);
                            double movement=(3.5+FootballSkills.Get(reactingKeeper.Player,"Goleiro")*.025)*Tick/TimeScale;
                            reactingKeeper.Z+=Math.Clamp(target-reactingKeeper.Z,-movement,movement);
                            Pose(reactingKeeper,target>reactingKeeper.Z?"dive_left":"dive_right",second,second+12);
                        }
                    }
                    physicalBall.AdvanceInPitch(Tick / (double)TimeScale);
                    ballX = physicalBall.X; ballY = physicalBall.Y; ballZ = physicalBall.Z;
                    touchingPlayers.RemoveWhere(index=>{
                        var player=agents[index];
                        return Math.Pow(ballX-player.X,2)+Math.Pow(ballZ-player.Z,2)>Math.Pow(ContactRadius(player)+BallPhysics.Radius+.08,2) || ballY>(player.Slot==0?2.15:1.7)+BallPhysics.Radius+.08;
                    });
                    var ballEnd=new Position3(ballX,ballY,ballZ);
                    var firstCollision=agents.Where(a=>a.Active && (a.Team!=flight.Team || flight.Delivery is "physical_pass" or "physical_cross") && (a.Index!=flight.Shooter || second-flight.Start>6) && !touchingPlayers.Contains(a.Index))
                        .Select(a=>(Agent:a, Fraction:BallPhysics.PlayerContactFraction(previousBall,ballEnd,a.X,a.Z,a.Slot==0?2.5:HeadHeight(a,second)+.05,ContactRadius(a))))
                        .Where(c=>c.Fraction!=null).OrderBy(c=>c.Fraction).ThenBy(c=>c.Agent.Index).FirstOrDefault();
                    int attackDirection = (flight.Team == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1);
                    if (physicalBall.CrossedGoalLine(previousX, attackDirection) && firstCollision.Agent==null)
                    {
                        flight = flight with { Result = "goal", X=ballX, Z=ballZ, StartY=ballY, EndY=ballY, Start=second-1, End = second, TargetX = ballX, TargetZ = ballZ, Reviewed=false };
                        physicalBall = null;
                    }
                    else if (firstCollision.Agent is Agent blocker)
                    {
                        touchingPlayers.Add(blocker.Index);
                        double fraction=firstCollision.Fraction!.Value;
                        ballX=physicalBall.X=previousBall.X+(ballX-previousBall.X)*fraction;
                        ballY=physicalBall.Y=previousBall.Y+(ballY-previousBall.Y)*fraction;
                        ballZ=physicalBall.Z=previousBall.Z+(ballZ-previousBall.Z)*fraction;
                        string contactType = ballY > 1.35 ? "cabeça" : ballY > .65 ? "corpo" : "perna";
                        double incomingSpeed=Math.Sqrt(physicalBall.VX*physicalBall.VX+physicalBall.VY*physicalBall.VY+physicalBall.VZ*physicalBall.VZ);
                        if(blocker.Team!=flight.Team)OpponentContact(blocker.Slot==0?OpponentBallPlay.DeliberateSave:OpponentBallPlay.Deflection);
                        if(blocker.Slot>0 && AerialPlay.Reachable(ballY,HeadHeight(blocker,second)))
                        {
                            if(blocker.Team==flight.Team && physicalOffside.Contains(blocker.Index))
                            {Event(second,"offside",blocker.Team,blocker.Player.Id,"Impedimento na disputa aérea.");possession=1-blocker.Team;carrier=Nearest(possession,ballX,ballZ).Index;physicalBall=null;flight=null;physicalOffside.Clear();kickoffUntil=decisionAt=second+12;Capture(second);continue;}
                            double direction=(blocker.Team==0?1:-1)*(second>=halfEnd?-1:1);
                            bool shot=blocker.X*direction>30 && Math.Abs(blocker.Z)<18;
                            var mate=agents.Where(a=>a.Active && a.Team==blocker.Team && a.Index!=blocker.Index).OrderBy(a=>Math.Pow(a.X-blocker.X-direction*10,2)+Math.Pow(a.Z-blocker.Z,2)).ThenBy(a=>a.Index).First();
                            double pressure=agents.Count(a=>a.Active && a.Team!=blocker.Team && Math.Pow(a.X-blocker.X,2)+Math.Pow(a.Z-blocker.Z,2)<4);
                            double skill=FootballSkills.Get(blocker.Player,"Finalização");
                            double error=(rng.NextDouble()-rng.NextDouble())*(1+(100-skill)*.035+pressure*.3);
                            var aim=shot?new Position3(direction*52.8,.9,error):new Position3(mate.X,1,mate.Z+error);
                            var impulse=BallDelivery.Launch(new(ballX,ballY,ballZ),aim,shot?.7:1.2);
                            physicalBall=impulse;physicalContact=RecordContact(blocker.Player.Id,blocker.Team,"cabeceio",second,ballX,ballY,ballZ,shot?"finalização":"passe aéreo");
                            Event(second,"header",blocker.Team,blocker.Player.Id,shot?"Cabeceio físico para o gol.":blocker.Team==flight.Team?"Cabeceio físico para companheiro.":"Cabeceio defensivo com impulso físico.",shot?null:mate.Player.Id);
                            Pose(blocker,"header",second,second+10);
                            physicalOffside.Clear();
                            foreach(var candidate in agents.Where(a=>a.Active && a.Team==blocker.Team && a.Index!=blocker.Index))if(OffsideSnapshot.Capture(candidate.X,candidate.Yaw,ballX,direction,agents.Where(a=>a.Active && a.Team!=blocker.Team).Select(a=>(a.X,a.Yaw)),false).IsOffence(true))physicalOffside.Add(candidate.Index);
                            flight=flight with{Team=blocker.Team,Shooter=blocker.Index,ShooterPlayer=blocker.Player,Start=second,Delivery=shot?"header":"physical_pass",Result="physical"};Capture(second);continue;
                        }
                        if(flight.Delivery is "physical_pass" or "physical_cross" && blocker.Slot>0)
                        {
                            if(blocker.Team==flight.Team && physicalOffside.Contains(blocker.Index))
                            {
                                Event(second,"offside",blocker.Team,blocker.Player.Id,"Impedimento confirmado por participação após o passe físico.");
                                possession=1-blocker.Team;carrier=Nearest(possession,ballX,ballZ).Index;
                                physicalBall=null;flight=null;physicalOffside.Clear();kickoffUntil=second+12;decisionAt=kickoffUntil;Capture(second);continue;
                            }
                            if(ballY<.8 && incomingSpeed<=8+FootballSkills.Get(blocker.Player,"Primeiro toque")*.2)
                            {
                                bool same=blocker.Team==flight.Team;
                                if(!same)OpponentContact(OpponentBallPlay.DeliberatePlay);
                                physicalContact=RecordContact(blocker.Player.Id,blocker.Team,same?"recepção":"interceptação",second,ballX,ballY,ballZ,flight.Delivery);
                                Event(second,same?"pass":"interception",blocker.Team,same?flight.ShooterPlayer.Id:blocker.Player.Id,same?"Recepção por contato após passe físico.":"Interceptação por contato com a trajetória física.",same?blocker.Player.Id:null);
                                carrier=blocker.Index;possession=blocker.Team;physicalBall=null;flight=null;physicalOffside.Clear();decisionAt=second+12;Capture(second);continue;
                            }
                        }
                        bool holds=blocker.Slot==0 && GoalkeeperHandling.CanHold(incomingSpeed,ballY,Math.Abs(ballZ-blocker.Z),FootballSkills.Get(blocker.Player,"Goleiro"));
                        physicalContact = RecordContact(blocker.Player.Id, blocker.Team, blocker.Slot == 0 ? holds ? "encaixe" : "espalmada" : contactType, second, ballX, ballY, ballZ, flight.Delivery);
                        if (holds)
                        {
                            carrier=blocker.Index; possession=blocker.Team;
                            Pose(blocker,"catch",second,second+24);
                            Event(second,"keeper_catch",possession,blocker.Player.Id,"O goleiro encaixa a bola.");
                            physicalBall=null; flight=null; decisionAt=second+24;
                            Capture(second); continue;
                        }
                        if(blocker.Slot==0 && ballY>1.8)
                        {
                            double outDirection=(blocker.Team==0?1:-1)*(second>=halfEnd?-1:1);
                            physicalBall.VX=outDirection*(8+FootballSkills.Get(blocker.Player,"Goleiro")*.06);physicalBall.VY=4;physicalBall.VZ=(ballZ-blocker.Z)*5;
                            physicalContact=RecordContact(blocker.Player.Id,blocker.Team,"soco",second,ballX,ballY,ballZ,flight.Delivery);
                            Event(second,"keeper_punch",blocker.Team,blocker.Player.Id,"Goleiro soca a bola após contato aéreo.");Pose(blocker,"air_duel",second,second+12);
                            flight=flight with{Start=second};Capture(second);continue;
                        }
                        double nx=ballX-blocker.X,nz=ballZ-blocker.Z;
                        double normalLength=Math.Sqrt(nx*nx+nz*nz);
                        if(normalLength<1e-8){nx=-Math.Sign(physicalBall.VX);nz=0;}else{nx/=normalLength;nz/=normalLength;}
                        double normalSpeed=physicalBall.VX*nx+physicalBall.VZ*nz;
                        if(normalSpeed<0){physicalBall.VX-=1.48*normalSpeed*nx;physicalBall.VZ-=1.48*normalSpeed*nz;}
                        physicalBall.VY = Math.Max(physicalBall.VY, 1.2);
                        if(continuousContacts)
                        {
                            // V12: the deflected ball uses the rest of the tick. Without this, a ball that starts
                            // a tick touching a player collides at fraction 0, returns to its previous position and
                            // can hover while players converge on it.
                            // If the remainder would cross a goal line, keep the contact point so the next tick's
                            // goal-line check still sees the crossing.
                            double remainingSeconds=(1-fraction)*Tick/(double)TimeScale;
                            var probe=new BallPhysics(ballX,ballY,ballZ,physicalBall.VX,physicalBall.VY,physicalBall.VZ,physicalBall.Spin);
                            probe.AdvanceInPitch(remainingSeconds);
                            if(Math.Abs(probe.X)<52.5-BallPhysics.Radius)
                            {
                                physicalBall.AdvanceInPitch(remainingSeconds);
                                ballX=physicalBall.X;ballY=physicalBall.Y;ballZ=physicalBall.Z;
                            }
                        }
                        bool deflectedPass=continuousContacts && blocker.Slot>0 && flight.Delivery is "physical_pass" or "physical_cross";
                        Event(second, "rebound", blocker.Team, blocker.Player.Id, blocker.Slot == 0 ? "O goleiro espalma a finalização." : deflectedPass ? $"A bola desvia em {blocker.Player.Name}." : "Finalização desviada pela defesa.");
                        Pose(blocker, blocker.Slot == 0 ? "dive_low" : "challenge", second, second+12);
                        flight = flight with { Start=second };
                        Capture(second);
                        continue;
                    }
                    else if (Math.Abs(ballX) <= 52.5 && Math.Abs(ballZ) <= 34 &&
                             ballY < .8 && Math.Sqrt(physicalBall.VX*physicalBall.VX + physicalBall.VZ*physicalBall.VZ) < 5)
                    {
                        // Both teams pursue a loose ball. Possession only changes once a
                        // player reaches it; stable ordering resolves simultaneous arrivals.
                        var contenders=agents.Where(a=>a.Active && a.Slot>0)
                            .OrderBy(a=>Math.Pow(a.X-ballX,2)+Math.Pow(a.Z-ballZ,2))
                            .ThenBy(a=>a.Index).Take(4).ToArray();
                        foreach (var contender in contenders)
                        {
                            double dx=ballX-contender.X, dz=ballZ-contender.Z;
                            double distance=Math.Sqrt(dx*dx+dz*dz);
                            if (distance > .45)
                            {
                                double step=Math.Min(distance-.45, .65);
                                contender.X=Math.Clamp(contender.X+dx/distance*step,-52.5,52.5);
                                contender.Z=Math.Clamp(contender.Z+dz/distance*step,-34,34);
                                contender.Yaw=Math.Atan2(dx,dz);
                                contender.Action="run";
                            }
                        }
                        var closest=contenders.FirstOrDefault(a=>Math.Pow(a.X-ballX,2)+Math.Pow(a.Z-ballZ,2)<.53*.53);
                        if (closest != null)
                        {
                            if(closest.Team==flight.Team && physicalOffside.Contains(closest.Index))
                            {
                                Event(second,"offside",closest.Team,closest.Player.Id,"Impedimento por participação na recuperação da bola física.");
                                possession=1-closest.Team;carrier=Nearest(possession,ballX,ballZ).Index;
                                physicalBall=null;flight=null;physicalOffside.Clear();kickoffUntil=second+12;decisionAt=kickoffUntil;Capture(second);continue;
                            }
                            carrier=closest.Index; possession=closest.Team;
                            physicalOffside.Clear();
                            physicalContact=RecordContact(closest.Player.Id,closest.Team,"controle",second,ballX,ballY,ballZ,"rebote");
                            Event(second,"rebound_recovery",possession,closest.Player.Id,$"{closest.Player.Name} recupera a bola após o rebote.");
                            decisionAt=second+12;
                            physicalBall=null; flight=null;
                        }
                        Capture(second);
                        continue;
                    }
                    else if (BallRestarts.IsOut(ballX,ballZ))
                    {
                        if (physicalContact != null)
                        {
                            int homeDirection = second >= halfEnd ? -1 : 1;
                            var restart = BallRestarts.Resolve(ballX, ballZ, physicalContact, homeDirection);
                            possession = restart.Team;
                            if (restart.Type == BallRestart.Corner)
                            {
                                carrier = Nearest(possession, ballX, ballZ).Index;
                                agents[carrier].X = Math.Sign(ballX)*52;
                                agents[carrier].Z = Math.Sign(ballZ == 0 ? 1 : ballZ)*33.5;
                                Event(second, "corner", possession, agents[carrier].Player.Id, "Desvio para a linha de fundo. Escanteio.");
                                ballX=agents[carrier].X; ballZ=agents[carrier].Z; ballY=.24;
                                pendingPhysicalRestart=BallRestart.Corner;
                                physicalRestartSpot=new(ballX,BallPhysics.Radius,ballZ);
                            kickoffUntil=second+24; decisionAt=kickoffUntil; offsideExempt=true;
                                physicalBall=null; flight=null; Capture(second); continue;
                            }
                            if (restart.Type == BallRestart.ThrowIn)
                            {
                                carrier=Nearest(possession,ballX,ballZ).Index;
                                agents[carrier].X=Math.Clamp(ballX,-51.5,51.5);
                                agents[carrier].Z=Math.Sign(ballZ)*33.9;
                                Event(second,"throw_in",possession,agents[carrier].Player.Id,"Bola pela lateral. Arremesso lateral.");
                                throwCarrier=carrier; throwRelease=second+36;
                                Pose(agents[carrier],"throw_in",throwRelease,throwRelease+24);
                                ballX=agents[carrier].X; ballZ=Math.Sign(ballZ)*34.4; ballY=1.95;
                                pendingPhysicalRestart=BallRestart.ThrowIn;
                                physicalRestartSpot=new(ballX,1.95,Math.Sign(ballZ)*34);
                                kickoffUntil=throwRelease; decisionAt=kickoffUntil; offsideExempt=true;
                                physicalBall=null; flight=null; Capture(second); continue;
                            }
                            carrier=possession*11; EnsureCarrier();
                            agents[carrier].X=Math.Sign(ballX)*47; agents[carrier].Z=ballZ>=0?6:-6;
                            ballX=agents[carrier].X; ballZ=agents[carrier].Z; ballY=.24;
                            Event(second,"goal_kick",possession,agents[carrier].Player.Id,"Tiro de meta.");
                            pendingPhysicalRestart=BallRestart.GoalKick;
                            physicalRestartSpot=new(ballX,BallPhysics.Radius,ballZ);
                            kickoffUntil=second+24; decisionAt=kickoffUntil; offsideExempt=true;
                            physicalBall=null; flight=null; Capture(second); continue;
                        }
                        flight = flight with { Result = "miss", X=ballX, Z=ballZ, StartY=ballY, EndY=ballY, Start=second-1, End = second, TargetX = ballX, TargetZ = ballZ, Reviewed=false };
                        physicalBall = null;
                    }
                    else
                    {
                        if (physicalBall.FrameHit && !flight.Reviewed)
                        {
                            Event(second, "woodwork", flight.Team, flight.ShooterPlayer.Id, "A bola bate na estrutura do gol!");
                            flight = flight with { Reviewed = true };
                        }
                        Capture(second);
                        continue;
                    }
                }
                double progress = Math.Clamp((second - flight.Start) / (double)(flight.End - flight.Start), 0, 1);
                if (!physicalShots || flight.Result is not ("goal" or "miss") || flight.Start != second)
                {
                ballX = flight.X + (flight.TargetX - flight.X) * progress;
                ballZ = flight.Z + (flight.TargetZ - flight.Z) * progress;
                ballY = flight.StartY + (flight.EndY - flight.StartY) * progress + Math.Sin(progress * Math.PI) * (flight.Reviewed ? 0 : flight.Delivery == "cross" ? 1.3 : flight.Result == "pass" ? .3 : 1.3);
                }
                if (!flight.Reviewed && ballY is > .8 and < 1.65)
                {
                    var armContact = agents.Where(a => a.Active && a.Team != flight.Team && a.Slot > 0 && a.Action is "challenge" or "air_duel")
                        .FirstOrDefault(a => Math.Pow(a.X-ballX,2)+Math.Pow(a.Z-ballZ,2)<.5*.5);
                    if (armContact != null && duels.NextDouble() < .06 && RefereeDecisions.Handball(false, true, false, false))
                    {
                        bool penalty = MatchRules.InPenaltyArea(ballX, ballZ, (flight.Team == 0 ? 1 : -1)*(second>=halfEnd ? -1 : 1));
                        Event(second,"handball",armContact.Team,armContact.Player.Id,"Toque no braço aberto amplia o corpo de forma antinatural.");
                        possession = flight.Team; carrier = Nearest(possession,ballX,ballZ).Index;
                        var taker=agents[carrier]; taker.X=ballX;taker.Z=ballZ;
                        Event(second,penalty ? "penalty" : "free_kick",possession,taker.Player.Id,penalty ? "Pênalti por mão na bola." : "Tiro livre direto por mão na bola.");
                        penaltyKick=penalty;offsideExempt=false;
                        if(penalty){(halfEnd,duration)=EnsurePenaltyTime(second,halfEnd,duration);taker.X=((possession==0?1:-1)*(second>=halfEnd?-1:1))*41.5;taker.Z=0;lastShot=second-150;}
                        flight=null;kickoffUntil=second+(penalty?36:12);decisionAt=kickoffUntil;if(penalty)Pose(taker,"penalty_kick",kickoffUntil,kickoffUntil+30);Capture(second);continue;
                    }
                }
                if (second >= flight.End)
                {
                    if (flight.Result == "goal" && !flight.Reviewed)
                    {
                        Event(second, "var_check", flight.Team, flight.ShooterPlayer.Id, "Checagem do gol: posição dos atacantes e interferência na visão do goleiro.");
                        flight = flight with { Reviewed = true, X = ballX, Z = ballZ, StartY = ballY, EndY = ballY, Start = second, End = second + 36 };
                        if(second<halfEnd && second+38>=halfEnd){var extra=second+38-halfEnd;halfEnd+=extra;duration+=extra;}
                        duration = Math.Max(duration, second + 38);
                        Capture(second); continue;
                    }
                    if (flight.Result == "goal" && flight.Infringement.Length > 0)
                    {
                        Event(second, "var_overturned", flight.Team, flight.ShooterPlayer.Id, "Gol anulado: atacante impedido obstruiu a visão do goleiro.");
                        Event(second, "offside", flight.Team, flight.ShooterPlayer.Id, "Impedimento por interferência no adversário. Tiro livre indireto.");
                        possession = 1 - flight.Team; carrier = possession * 11; EnsureCarrier();
                        kickoffUntil = second + 12; decisionAt = kickoffUntil; ballX = agents[carrier].X; ballZ = agents[carrier].Z; ballY = .24;
                    }
                    else if (flight.Result == "goal")
                    {
                        Event(second, "var_confirmed", flight.Team, flight.ShooterPlayer.Id, "Gol confirmado após checagem.");
                        if (flight.Team == 0) homeGoals++; else awayGoals++;
                        Event(second, "goal", flight.Team, flight.ShooterPlayer.Id, flight.Delivery == "header" ? $"Gol de cabeça de {flight.ShooterPlayer.Name}!" : $"Gol de {flight.ShooterPlayer.Name}!");
                        foreach (var a in agents.Where(a => a.Active && a.Team == flight.Team)) { Pose(a, a.Player.Id % 3 == 0 ? "celebrate_2" : a.Player.Id % 3 == 1 ? "celebrate_3" : "celebrate",second,second+48); }
                        Capture(second);
                        possession = 1 - flight.Team; carrier = possession * 11 + 6; EnsureCarrier(); kickoffUntil = second + 24; decisionAt = kickoffUntil;
                        ballX = ballZ = 0; ballY = .24;
                    }
                    else if (flight.Result == "throw_in")
                    {
                        possession = 1 - flight.Team; carrier = Nearest(possession, flight.TargetX, Math.Clamp(flight.TargetZ, -33, 33)).Index;
                        agents[carrier].X = flight.TargetX; agents[carrier].Z = Math.Sign(flight.TargetZ) * 33.9;
                        Event(second, "throw_in", possession, agents[carrier].Player.Id, "Bola pela lateral. Arremesso lateral.");
                        throwCarrier=carrier; throwRelease=second+36;
                        agents[carrier].Yaw=flight.TargetZ>0?Math.PI:0;
                        Pose(agents[carrier],"throw_in",throwRelease,throwRelease+24);
                        ballX=agents[carrier].X;ballZ=Math.Sign(flight.TargetZ)*34.4;ballY=1.95;
                        kickoffUntil = throwRelease; decisionAt = kickoffUntil; offsideExempt = true;
                        // A pass leaving the touchline must use the same physical
                        // throw delivery as a shot or deflection leaving it.
                        if (physicalShots) { pendingPhysicalRestart = BallRestart.ThrowIn; physicalRestartSpot=new(ballX,1.95,Math.Sign(ballZ)*34); }
                    }
                    else if (flight.Result is "save" or "miss")
                    {
                        var keeper = agents[(1 - flight.Team) * 11]; if(keeper.Action is not ("dive_low" or "dive_left" or "dive_right"))Pose(keeper, flight.Result == "save" ? "catch" : "keeper_ready", second, second + 6);
                        Event(second, "shot", flight.Team, flight.ShooterPlayer.Id, flight.Result == "save" ? "O goleiro faz a defesa." : "Finalização para fora.");
                        if(intelligence!=null&&flight.Result=="save"&&rules.NextDouble()<.30){
                            double dir=(flight.Team==0?1:-1)*(second>=halfEnd?-1:1),rx=ClampX(flight.TargetX-dir*(5+rules.NextDouble()*5)),rz=ClampZ(flight.TargetZ+(rules.NextDouble()-.5)*10);
                            var winner=agents.Where(a=>a.Active&&a.Slot>0).MinBy(a=>Math.Sqrt(Math.Pow(a.X-rx,2)+Math.Pow(a.Z-rz,2))/(.7+FootballSkills.Get(a.Player,"Antecipação")/100))!;
                            Event(second,"rebound",keeper.Team,keeper.Player.Id,"Defesa parcial do goleiro. A bola continua viva para a disputa do rebote.");
                            flight=new(ballX,ballZ,rx,rz,second,second+FlightTicks(Math.Sqrt(Math.Pow(rx-ballX,2)+Math.Pow(rz-ballZ,2)),11),winner.Index,"pass",carrier,possession,agents[carrier].Player,"rebound");Capture(second);continue;
                        }
                        if (flight.Result == "save" && rules.NextDouble() < .25)
                        {
                            possession = flight.Team; carrier = Nearest(possession, flight.TargetX, 33).Index;
                            agents[carrier].X = Math.Sign(flight.TargetX) * 51.5; agents[carrier].Z = 33;
                            Event(second, "corner", possession, agents[carrier].Player.Id, "Defesa para a linha de fundo. Escanteio.");
                            offsideExempt = true; kickoffUntil = second + 10; decisionAt = kickoffUntil;
                        }
                        else
                        {
                            possession = 1 - flight.Team; carrier = possession * 11; EnsureCarrier(); decisionAt = second + 10;
                            if (flight.Result == "miss") { Event(second, "goal_kick", possession, agents[carrier].Player.Id, "Tiro de meta."); offsideExempt = true; }
                        }
                    }
                    else
                    {
                        if (flight.OffsideRecipient)
                        {
                            Event(second, "offside", flight.Team, agents[flight.Recipient].Player.Id, "Impedimento: o receptor participa da jogada. Tiro livre indireto.");
                            possession = 1 - flight.Team; carrier = Nearest(possession, ballX, ballZ).Index;
                            agents[carrier].X = ballX; agents[carrier].Z = ballZ;
                            kickoffUntil = second + 12; decisionAt = kickoffUntil; flight = null;
                            Capture(second); continue;
                        }
                        carrier = flight.Recipient; if (!agents[carrier].Active) carrier = Nearest(1 - flight.Team, ballX, ballZ).Index; possession = agents[carrier].Team;
                        if (physicalShots) physicalContact = RecordContact(agents[carrier].Player.Id, possession, possession == flight.Team ? "recepção" : "interceptação", second, ballX, ballY, ballZ, flight.Delivery);
                        Event(second, flight.Delivery=="rebound"?"rebound_recovery":possession == flight.Team ? "pass" : "interception", possession, possession == flight.Team ? flight.ShooterPlayer.Id : agents[carrier].Player.Id,
                            flight.Delivery=="rebound"?$"{agents[carrier].Player.Name} chega primeiro ao rebote.":possession == flight.Team ? $"{flight.ShooterPlayer.Name} encontra {agents[carrier].Player.Name} no passe." : $"{agents[carrier].Player.Name} intercepta a jogada.", possession == flight.Team ? agents[carrier].Player.Id : null);
                        decisionAt = second + 12 + rng.Next(0, 7) * Tick;
                        if(intelligence!=null){intelligence.Update(Observe(second));intelligence.PassArrived(flight.Shooter,carrier,second);decisionAt=second+intelligence.DecisionDelay(carrier);}
                        headerCarrier = possession == flight.Team && flight.Delivery == "cross" ? carrier : -1;
                        if (headerCarrier >= 0) decisionAt = second;
                    }
                    flight = null;
                }
            }
            if (flight == null)
            {
                for (int team = 0; team < 2; team++)
                    foreach (var plan in teams[team].Substitutions ?? [])
                    {
                        int plannedSecond = plan.Minute * 60 + (plan.Minute >= 45 ? halfEnd - 2700 : 0);
                        if (second < plannedSecond || !donePlans.Add((team, plan.OutPlayerId))) continue;
                        var outgoing = agents.FirstOrDefault(a => a.Active && a.Team == team && a.Player.Id == plan.OutPlayerId);
                        var incoming = unused[team].Find(p => p.Id == plan.InPlayerId);
                        if (outgoing == null || incoming == null || !Replace(outgoing, incoming, second, "Troca programada.")) Event(second, "substitution_cancelled", team, plan.OutPlayerId, "Troca programada cancelada: atleta indisponível ou limite atingido.", plan.InPlayerId);
                    }
                if (second % 60 == 0 && second < 5100)
                    for (int team = 0; team < 2; team++)
                    {
                        var active = agents.Where(a => a.Active && a.Team == team).ToArray();
                        if (injuryOccurred[team] || active.Length <= 7 || medical.NextDouble() > .003 + Math.Max(0, 80 - active.Average(a => a.Player.Fitness)) * .0007) continue;
                        injuryOccurred[team] = true;
                        var injured = active[medical.Next(active.Length)]; int days = new[] { 7, 14, 21, 28 }[medical.Next(4)];
                        Event(second, "injury", team, injured.Player.Id, $"{injured.Player.Name} deixa a partida com desconforto muscular. Previsão: {days} dias.", injuryDays: days);
                        var reserve = teams[team].AutoSubstitute ? (Reserve(injured) ?? unused[team].FirstOrDefault(p => p.Position != "GOL")) : null;
                        if (reserve == null || !Replace(injured, reserve, second, "Substituição por lesão.")) Remove(injured, second);
                    }
                if ((!physicalShots || pendingPhysicalRestart == null) && second % 6 == 0 && second < duration - 10 && second >= kickoffUntil && discipline.NextDouble() < (MatchRules.InPenaltyArea(agents[carrier].X, agents[carrier].Z, (possession == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1)) ? .10 : .14)*(intelligence==null?1:MatchRules.InPenaltyArea(agents[carrier].X,agents[carrier].Z,(possession==0?1:-1)*(second>=halfEnd?-1:1))?.12:.28)*(intelligence!=null&&teams[1-possession].Plan?.AvoidFouls==true?.55:1))
                {
                    var offender = Nearest(1 - possession, ballX, ballZ); var victim = agents[carrier];
                    if (Math.Pow(offender.X - victim.X, 2) + Math.Pow(offender.Z - victim.Z, 2) <= 2.75 * 2.75)
                    {
                        Event(second, "foul", offender.Team, offender.Player.Id, $"Falta de {offender.Player.Name} em {victim.Player.Name}.", victim.Player.Id);
                        var attackDirection = (possession == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1);
                        bool penaltyArea = MatchRules.InPenaltyArea(victim.X, victim.Z, attackDirection);
                        var relativeSpeed=Math.Sqrt(Math.Pow(offender.VelocityX-victim.VelocityX,2)+Math.Pow(offender.VelocityZ-victim.VelocityZ,2));
                        var fromBehind=(offender.X-victim.X)*Math.Sin(victim.Yaw)+(offender.Z-victim.Z)*Math.Cos(victim.Yaw)<-.5;
                        var severity=Math.Clamp(relativeSpeed/13+(fromBehind?.18:0)+discipline.NextDouble()*.25,0,1);
                        bool dogso = victim.X * attackDirection > 30 && Math.Abs(victim.Z) < 14 && agents.Count(a => a.Active && a.Team == offender.Team && a.X * attackDirection > victim.X * attackDirection) <= 1;
                        bool attemptsBall = Math.Sqrt(Math.Pow(offender.X - victim.X, 2) + Math.Pow(offender.Z - victim.Z, 2)) < 2;
                        var sanction = FootballDecisions.Foul(severity > .65, severity > .93, dogso, penaltyArea, attemptsBall, victim.X * attackDirection > 18);
                        bool directRed = sanction == FootballDecisions.Sanction.Red, yellow = sanction == FootballDecisions.Sanction.Yellow;
                        int activeCount = agents.Count(a => a.Active && a.Team == offender.Team);
                        if (activeCount > 7 && (directRed || yellow && bookings.GetValueOrDefault(offender.Player.Id) > 0))
                        {
                            if (!directRed) Event(second, "second_yellow", offender.Team, offender.Player.Id, $"Segundo amarelo para {offender.Player.Name}.");
                            Event(second, "red_card", offender.Team, offender.Player.Id, $"{offender.Player.Name} é expulso."); Remove(offender, second);
                        }
                        else if (yellow && bookings.GetValueOrDefault(offender.Player.Id) == 0)
                        { bookings[offender.Player.Id] = 1; Event(second, "yellow_card", offender.Team, offender.Player.Id, $"Cartão amarelo para {offender.Player.Name}."); }
                        Pose(offender, "tackle", second, second + 24);
                        Pose(victim, "fall", second, second + 38);
                        bool penalty = MatchRules.InPenaltyArea(victim.X, victim.Z, (possession == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1));
                        bool playAdvantage = RefereeDecisions.Advantage(!penalty && sanction != FootballDecisions.Sanction.Red && !(yellow && bookings.GetValueOrDefault(offender.Player.Id) > 0), true, victim.X * attackDirection > 12);
                        if (playAdvantage)
                        {
                            Pose(victim, "shield", second, second + 14);
                            advantage = (second + 36, possession, victim.X, victim.Z, victim.Player.Id);
                            Event(second, "advantage", possession, victim.Player.Id, "O árbitro sinaliza vantagem e acompanha a continuidade do ataque.");
                        }
                        else
                        {
                        Event(second, penalty ? "penalty" : "free_kick", possession, victim.Player.Id, penalty ? "Pênalti assinalado." : "Tiro livre direto para a equipe que sofreu a falta.");
                        offsideExempt = false; penaltyKick = penalty; if (penalty) (halfEnd, duration) = EnsurePenaltyTime(second, halfEnd, duration);
                        if (penalty) { victim.X = ((possession == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1)) * 41.5; victim.Z = 0; lastShot = second - 150; }
                        kickoffUntil = second + (penalty ? 36 : 8); decisionAt = kickoffUntil;
                        if(penalty)Pose(victim,"penalty_kick",kickoffUntil,kickoffUntil+36);
                        }
                    }
                }
                if (intelligence==null && second is 3600 or 4200 or 4800)
                    for (int team = 0; team < 2; team++)
                    {
                        if (!teams[team].AutoSubstitute || substitutions[team] >= 5) continue;
                        var tired = agents.Where(a => a.Active && a.Team == team && a.Slot > 0).OrderBy(a => a.Player.Fitness - secondsPlayed.GetValueOrDefault(a.Player.Id) / 60 * .20).FirstOrDefault(a => Reserve(a) != null);
                        if (tired != null && tired.Player.Fitness - secondsPlayed.GetValueOrDefault(tired.Player.Id) / 60 * .20 < 88) Replace(tired, Reserve(tired)!, second, "Troca automática para renovar o fôlego.");
                    }
                if(intelligence!=null&&second>=3300&&second%600==0){intelligence.Update(Observe(second));for(int team=0;team<2;team++){if(teams[team].ComputerControlled||!teams[team].AutoSubstitute||substitutions[team]>=5)continue;var tired=agents.Where(a=>a.Active&&a.Team==team&&a.Slot>0&&Reserve(a)!=null).OrderBy(a=>intelligence.Fitness(ObserveAgent(a))).FirstOrDefault();if(tired!=null&&intelligence.Fitness(ObserveAgent(tired))<68)Replace(tired,Reserve(tired)!,second,"Troca automática por desgaste.");}}
            }
            if (advantage is { } pending)
            {
                if (possession != pending.Team)
                {
                    Event(second, "advantage_recalled", pending.Team, pending.Victim, "A vantagem não se concretiza. Falta original marcada.");
                    possession = pending.Team; carrier = Nearest(possession, pending.X, pending.Z).Index;
                    agents[carrier].X = pending.X; agents[carrier].Z = pending.Z;
                flight=null; if(physicalShots)physicalOffside.Clear();
                        headerCarrier = -1; offsideExempt = false; penaltyKick = false; kickoffUntil = second + 12; decisionAt = kickoffUntil;
                    Event(second, "free_kick", pending.Team, pending.Victim, "Tiro livre direto no local da falta original.");
                    advantage = null;
                }
                else if (second >= pending.Until || flight?.Result is "goal" or "save" or "miss") advantage = null;
            }
            EnsureCarrier();
            intelligence?.Update(Observe(second));
            if(intelligence!=null&&flight==null&&second>=kickoffUntil){for(int team=0;team<2;team++){var change=intelligence.Coach(team,second,unused[team],substitutions[team]);if(change==null)continue;teams[team]=teams[team] with{Formation=change.Formation,Mentality=change.Mentality,Plan=change.Plan};slots[team]=Slots(change.Formation,(team==0?1:-1)*(second>=halfEnd?-1:1));Event(second,"tactics_change",team,0,"Ajuste do adversário: "+change.Reason);if(change.OutPlayer!=0){var outgoing=agents.FirstOrDefault(a=>a.Active&&a.Team==team&&a.Player.Id==change.OutPlayer);var fresh=unused[team].Find(p=>p.Id==change.InPlayer);if(outgoing!=null&&fresh!=null)Replace(outgoing,fresh,second,"Leitura do desgaste e do setor.");}}intelligence.Update(Observe(second));}
            // Forma coletiva, pressão ao portador e ocupação de espaço são calculadas no servidor.
            var nearestDefender = Nearest(1 - possession, ballX, ballZ);
            double attackSide = (possession == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1);
            double attackingLine = agents.Where(a => a.Active && a.Team != possession).Select(a => a.X * attackSide).OrderByDescending(x => x).Skip(1).First();
            var marks = new Dictionary<int, Agent>(); var marked = new HashSet<int>();
            foreach (var defender in agents.Where(a => a.Active && a.Team != possession && a.Slot > 0 && a != nearestDefender).OrderBy(a => a.Slot))
            {
                var rival = agents.Where(a => a.Active && a.Team == possession && a.Slot > 0 && a.Index != carrier && !marked.Contains(a.Index))
                    .MinBy(a => Math.Pow(a.X - defender.X, 2) + Math.Pow(a.Z - defender.Z, 2));
                if (rival != null) { marks[defender.Index] = rival; marked.Add(rival.Index); }
            }
            foreach (var a in agents)
            {
                if (!a.Active) continue;
                double direction = (a.Team == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1);
                var slot = slots[a.Team][a.Slot];
                double push = teams[a.Team].Mentality == "Ofensiva" ? 8 : teams[a.Team].Mentality == "Defensiva" ? -6 : 0;
                double targetX = a.Slot == 0 ? direction * -49 : slot.X + ballX * .34 + direction * (a.Team == possession ? 5 + push : -3);
                double targetZ = a.Slot == 0 ? ballZ * .12 : slot.Z + ballZ * .24;
                if (a.Team != possession && marks.TryGetValue(a.Index, out var markedPlayer))
                {
                    targetX = targetX * .45 + (markedPlayer.X + direction * -1.4) * .55;
                    targetZ = targetZ * .35 + markedPlayer.Z * .65;
                }
                if (a.Team == possession && a.Slot > 0 && a.Index != carrier && flight == null) targetX = direction * Math.Min(targetX * direction, Math.Max(attackingLine, ballX * direction) - .8);
                if (kickoffUntil > second) { targetX = a.Index == carrier ? a.X : slot.X; targetZ = a.Index == carrier ? a.Z : slot.Z; }
                else if (a == agents[carrier] && flight == null) { targetX = a.X + direction * 10; targetZ = a.Z * .92; }
                else if (a == nearestDefender) { targetX = ballX - direction * 2; targetZ = ballZ; }
                else if (flight?.Recipient == a.Index) { targetX = flight.TargetX; targetZ = flight.TargetZ; }
                var intelligentTarget=intelligence!=null&&second>=kickoffUntil?intelligence.Target(ObserveAgent(a)):null;
                if(intelligentTarget!=null){targetX=intelligentTarget.X;targetZ=intelligentTarget.Z;}
                var dx = ClampX(targetX) - a.X; var dz = ClampZ(targetZ) - a.Z;
                var distance = Math.Sqrt(dx * dx + dz * dz);
                var fatigue = Math.Clamp(1 - secondsPlayed.GetValueOrDefault(a.Player.Id) / 5400.0 * .16, .75, 1);
                var sprint = a.Index == carrier || a == nearestDefender || flight?.Recipient == a.Index;
                var topSpeed = (sprint ? 6.3 + Rating(a.Player) / 65 : 3.5 + Rating(a.Player) / 110) * fatigue;
                if(intelligence!=null)topSpeed=intelligence.TopSpeed(ObserveAgent(a),intelligentTarget?.Sprint??sprint);
                var physicalDelta = Tick / (double)TimeScale;
                if((a.Index==throwCarrier && second<=throwRelease)||(penaltyKick && a.Index==carrier)){targetX=a.X;targetZ=a.Z;dx=0;dz=0;distance=0;a.VelocityX=a.VelocityZ=0;}
                var grounded = a.ActionUntil >= second && a.Action is "fall" or "tackle" or "air_duel";
                var striking = a.ActionUntil >= second && a.Action is "pass" or "shot" or "cross" or "header";
                var speed = Math.Min(grounded ? 0 : striking ? 1 : topSpeed, distance / physicalDelta);
                var desiredX = distance > .01 ? dx / distance * speed : 0;
                var desiredZ = distance > .01 ? dz / distance * speed : 0;
                var changeX = desiredX - a.VelocityX; var changeZ = desiredZ - a.VelocityZ;
                var change = Math.Sqrt(changeX * changeX + changeZ * changeZ);
                var blend = change > 0 ? Math.Min(1, (intelligence?.Acceleration(ObserveAgent(a))??3.2) * physicalDelta / change) : 1;
                a.VelocityX += changeX * blend; a.VelocityZ += changeZ * blend;
                a.X = ClampX(a.X + a.VelocityX * physicalDelta); a.Z = a.Index == throwCarrier ? a.Z : ClampZ(a.Z + a.VelocityZ * physicalDelta);
                if (!striking && !grounded && Math.Abs(a.VelocityX) + Math.Abs(a.VelocityZ) > .05) a.Yaw = Math.Atan2(a.VelocityX, a.VelocityZ);
                if (a.ActionUntil < second)
                {
                    var action = a.Slot == 0 ? "keeper_ready" : a.Team != possession && a == nearestDefender ? "defend" : a.Index == carrier && flight == null && second >= kickoffUntil ? "dribble" : distance > .5 ? "run" : "idle";
                    if (a.Action != action) { a.ActionSecond = second; a.Action = action; }
                }
                if (a.Slot == 0) a.Yaw = Math.Atan2(ballX - a.X, ballZ - a.Z);
            }
            for (int collisionPass = 0; collisionPass < 3; collisionPass++)
                for (int i = 0; i < agents.Length; i++) for (int j = i + 1; j < agents.Length; j++)
                {
                    var a = agents[i]; var b = agents[j]; if (!a.Active || !b.Active) continue;
                    var separated = MatchRules.Separate(a.X, a.Z, b.X, b.Z, .52);
                    a.X = ClampX(separated.Ax); a.Z = ClampZ(separated.Az); b.X = ClampX(separated.Bx); b.Z = ClampZ(separated.Bz);
                }
            if(penaltyKick && flight==null){var taker=agents[carrier];taker.X=attackSide*41.5;taker.Z=0;taker.Yaw=attackSide*Math.PI/2;taker.VelocityX=taker.VelocityZ=0;var keeper=agents[(1-possession)*11];keeper.X=attackSide*52.1;keeper.Z=0;keeper.VelocityX=keeper.VelocityZ=0;foreach(var a in agents.Where(a=>a.Active&&a.Index!=carrier&&a.Slot>0&&a.X*attackSide>30.35)){a.X=attackSide*30.35;a.VelocityX=0;}ballX=taker.X;ballZ=0;ballY=.24;}
            if(throwCarrier>=0 && flight==null && second<=throwRelease)agents[throwCarrier].Z=Math.Sign(agents[throwCarrier].Z)*33.9;
            if(throwCarrier>=0 && flight==null && second<throwRelease){ballX=agents[throwCarrier].X;ballZ=Math.Sign(agents[throwCarrier].Z)*34.4;ballY=1.95;}
            if(physicalShots && pendingPhysicalRestart!=null && physicalRestartSpot is Position3 spot)
            {
                // The taker cannot dribble away from the restart point while
                // the tactical movement and separation passes prepare others.
                var taker=agents[carrier];taker.X=spot.X;taker.Z=spot.Z;
                taker.VelocityX=taker.VelocityZ=0;
                ballX=spot.X;ballY=spot.Y;ballZ=spot.Z;
            }
            if (flight == null && !penaltyKick && (!physicalShots || pendingPhysicalRestart==null) && throwCarrier<0 && second >= kickoffUntil && second - lastDuel >= 30 && agents[carrier].Slot > 0)
            {
                var holder = agents[carrier]; var challenger = Nearest(1 - possession, holder.X, holder.Z);
                var separation = Math.Sqrt(Math.Pow(holder.X - challenger.X, 2) + Math.Pow(holder.Z - challenger.Z, 2));
                if (separation < 1.7 && challenger.ActionUntil < second && holder.Action is not "fall" && duels.NextDouble() < .4)
                {
                    lastDuel = second;
                    var facing = Math.Atan2(holder.X - challenger.X, holder.Z - challenger.Z);
                    challenger.Yaw = facing;
                    var chance = Math.Clamp(.42 + (Rating(challenger.Player) - Rating(holder.Player)) / 180, .2, .7);
                    if(intelligence!=null)chance=Math.Clamp(.35+(FootballSkills.Get(challenger.Player,"Defesa")+FootballSkills.Get(challenger.Player,"Antecipação")-FootballSkills.Get(holder.Player,"Drible")-FootballSkills.Get(holder.Player,"Força"))/220,.12,.72);
                    bool sliding = duels.NextDouble() < .3;
                    Pose(challenger, sliding ? "tackle" : "challenge", second, second + (sliding ? 26 : 14));
                    if (duels.NextDouble() < chance)
                    {
                        Event(second, "tackle", challenger.Team, challenger.Player.Id, $"{challenger.Player.Name} chega na bola e desarma {holder.Player.Name}.", holder.Player.Id);
                        Pose(holder, sliding ? "fall" : "stumble", second, second + (sliding ? 36 : 18));
                        possession = challenger.Team; carrier = challenger.Index; decisionAt = second + 30;
                        if (physicalShots) physicalContact = RecordContact(challenger.Player.Id, challenger.Team, "desarme", second, ballX, ballY, ballZ, "disputa");
                    }
                    else
                    {
                        Pose(holder, "shield", second, second + 18);
                        Event(second, "duel", holder.Team, holder.Player.Id, $"{holder.Player.Name} protege a bola e resiste à marcação.", challenger.Player.Id);
                    }
                }
            }
            if (flight == null && second >= kickoffUntil)
            {
                var owner = agents[carrier];
                var attackDirection = (possession == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1);
                var control = RefereeDecisions.ControlPoint(owner.X, owner.Z, owner.Yaw, second / (double)TimeScale, owner.Slot == 0);
                ballX = control.X; ballZ = control.Z; ballY = owner.Slot == 0 && owner.Action == "catch" && owner.ActionUntil >= second ? 1.15 : .24;
                if(physicalShots && pendingPhysicalRestart!=null && physicalRestartSpot is Position3 releaseSpot)
                {ballX=releaseSpot.X;ballY=releaseSpot.Y;ballZ=releaseSpot.Z;}
                if (second >= decisionAt && second < duration - 8 && !(owner.ActionUntil >= second && owner.Action is "tackle" or "fall"))
                {
                    intelligence?.Update(Observe(second));
                    var choice=intelligence?.Decide(carrier);
                    if (physicalShots && pendingPhysicalRestart != null)
                        choice=intelligence!.Choices(carrier).Where(c=>c.Recipient>=0 && c.Recipient!=carrier)
                            .OrderByDescending(c=>c.Utility).FirstOrDefault();
                    if(intelligence!=null&&(throwCarrier==carrier||choice?.Kind=="shot"&&second-lastShot<90&&!penaltyKick&&headerCarrier!=carrier))choice=intelligence.Choices(carrier).Where(c=>c.Kind is not("shot" or "dribble" or "shield")).OrderByDescending(c=>c.Utility).FirstOrDefault();
                    if(choice!=null&&throwCarrier!=carrier&&!penaltyKick&&headerCarrier!=carrier&&choice.Kind is "dribble" or "shield"){
                        Pose(owner,choice.Kind=="shield"?"shield":"dribble",second,second+8);decisionAt=second+intelligence!.DecisionDelay(carrier)+4;
                        if(intelligence.Announce(choice,owner.Player.Id,second))Event(second,"play_pattern",possession,owner.Player.Id,choice.Pattern+": "+owner.Player.Name+" mantém a bola sob controle.");Capture(second);continue;
                    }
                    double direction = (possession == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1);
                    var ownRating = agents.Where(a => a.Active && a.Team == possession).Average(a => Rating(a.Player));
                    var otherRating = agents.Where(a => a.Active && a.Team != possession).Average(a => Rating(a.Player));
                    if ((!physicalShots || pendingPhysicalRestart == null) && throwCarrier != carrier && owner.Slot > 0 && (penaltyKick || headerCarrier == carrier || (intelligence==null?owner.X * direction > 24 && Math.Abs(owner.Z) <= 8 && second - lastShot >= 150:choice?.Kind=="shot"&&second-lastShot>=90)))
                    {
                        var mentality = teams[possession].Mentality == "Ofensiva" ? .035 : teams[possession].Mentality == "Defensiva" ? -.025 : 0;
                        var defensiveBonus = teams[1 - possession].Mentality == "Defensiva" ? .035 : 0;
                        var probability = Math.Clamp(.09 + (ownRating - otherRating) / 380 + mentality - defensiveBonus + (60 - Rating(agents[(1 - possession) * 11].Player)) / 600, .035, .26);
                        if (penaltyKick) probability = .76;
                        bool penaltyTaken=penaltyKick;
                        lastShot = second; penaltyKick = false; offsideExempt = false;
                        var intelligentShot=physicalShots ? null : intelligence?.Shoot(carrier,penaltyTaken,headerCarrier==carrier,rng);
                        var result = intelligentShot?.Result??(rng.NextDouble() < probability ? "goal" : rng.NextDouble() < .7 ? "save" : "miss");
                        bool header = headerCarrier == carrier; headerCarrier = -1;
                        Pose(owner, penaltyTaken ? "penalty_kick" : header ? "header" : "shot", second, second + 30);
                        if(penaltyTaken)Event(second,"penalty_taken",possession,owner.Player.Id,$"{owner.Player.Name} cobra o pênalti.");
                        var keeper = agents[(1 - possession) * 11];
                        double targetX = direction * (result == "goal" ? 54 : 51), targetZ = result == "miss" ? 6 : rng.NextDouble() * 5 - 2.5;
                        if (result == "save") { targetX = keeper.X - direction * .45; targetZ = Math.Clamp(targetZ, keeper.Z - 1.35, keeper.Z + 1.35); }
                        if(intelligentShot!=null){targetX=intelligentShot.X;targetZ=intelligentShot.Z;}
                        int shotTicks = FlightTicks(Math.Sqrt(Math.Pow(targetX - owner.X, 2) + Math.Pow(targetZ - owner.Z, 2)), header ? 18 : 28);
                        var lateral = (targetX - keeper.X) * Math.Cos(keeper.Yaw) - (targetZ - keeper.Z) * Math.Sin(keeper.Yaw);
                        if(!delayedKeeper)Pose(keeper, result == "miss" ? "keeper_ready" : !header && Math.Abs(lateral)<.5 ? "dive_low" : Math.Abs(lateral)<.5 ? "catch" : lateral>0 ? "dive_left" : "dive_right", second + shotTicks, second + shotTicks + 24);
                        ballX = owner.X + direction * (header ? .12 : .5); ballY = header ? 1.85 : .24;
                        owner.Yaw = Math.Atan2(targetX - owner.X, targetZ - owner.Z);
                        var obstruction = agents.Any(a => a.Active && a.Team == possession && a.Index != carrier && FootballDecisions.Offside(a.X, a.Yaw, ballX, direction, agents.Where(d => d.Active && d.Team != possession).Select(d => (d.X, d.Yaw))) && RefereeDecisions.ObstructsView(a.X, a.Z, keeper.X, keeper.Z, ballX, ballZ));
                        if (physicalShots)
                        {
                            double skill = FootballSkills.Get(owner.Player, "Finalização");
                            double aim = (rng.NextDouble() - rng.NextDouble()) * (1.5 + (100 - skill) * .065);
                            double distance = Math.Max(1, direction * 52.5 - ballX * direction);
                            double speed = header ? 19 : 28;
                            double lateralSpeed = (aim - ballZ) / distance * speed;
                            double lift = header ? 1.5 : 1.2 + rng.NextDouble() * 4.5;
                            physicalBall = new(ballX, Math.Max(BallPhysics.Radius, ballY), ballZ, direction * speed, lift, lateralSpeed, (rng.NextDouble() - .5) * 12);
                            touchingPlayers.Clear();
                            physicalContact = RecordContact(owner.Player.Id, possession, header ? "cabeceio" : "chute", second, ballX, ballY, ballZ, "finalização");
                            result = "physical";
                        }
                        flight = new(ballX, ballZ, targetX, targetZ, second, second + shotTicks, (1 - possession) * 11, result, carrier, possession, owner.Player, header ? "header" : "ground", ballY, result == "save" ? header ? 1.4 : .35 : header ? 1.0 : .24, false, false, obstruction ? "offside_interference" : "");
                    }
                    else
                    {
                        var candidates = intelligence!=null?[]:agents.Select((a, i) => (Agent: a, Index: i)).Where(a => a.Agent.Active && a.Agent.Team == possession && a.Index != carrier && a.Agent.Slot > 0)
                            .OrderByDescending(a => a.Agent.X * direction + rng.NextDouble() * 12 - Math.Sqrt(Math.Pow(a.Agent.X - owner.X, 2) + Math.Pow(a.Agent.Z - owner.Z, 2)) * .22
                                + agents.Where(d => d.Active && d.Team != possession).Min(d => Math.Sqrt(Math.Pow(d.X - a.Agent.X, 2) + Math.Pow(d.Z - a.Agent.Z, 2))) * .7).Take(4).ToArray();
                        var recipient = intelligence!=null&&choice?.Recipient>=0&&choice.Recipient!=carrier?choice.Recipient:intelligence!=null?agents.Where(a=>a.Active&&a.Team==possession&&a.Index!=carrier).MinBy(a=>Math.Pow(a.X-owner.X,2)+Math.Pow(a.Z-owner.Z,2))!.Index:candidates[rng.Next(candidates.Length)].Index;
                        var receiver = agents[recipient];
                        var laneDefender = agents.Where(d => d.Active && d.Team != possession && d.Slot > 0).MinBy(d => FootballDecisions.PassingLaneDistance(d.X, d.Z, owner.X, owner.Z, receiver.X, receiver.Z))!;
                        var laneDistance = FootballDecisions.PassingLaneDistance(laneDefender.X, laneDefender.Z, owner.X, owner.Z, receiver.X, receiver.Z);
                        var accuracy = Math.Clamp(.84 + (ownRating - otherRating) / 500 - Math.Max(0, 3 - laneDistance) * .04, .65, .94);
                        if(intelligence!=null&&choice!=null)accuracy=intelligence.PassAccuracy(ObserveAgent(owner),choice);
                        if (!physicalShots && rng.NextDouble() > accuracy) recipient = laneDefender.Index;
                        var target = agents[recipient];
                        bool receiverOffside = target.Team == possession && (physicalShots
                            ? OffsideSnapshot.Capture(target.X, target.Yaw, ballX, direction,
                                agents.Where(a => a.Active && a.Team != possession).Select(a => (a.X, a.Yaw)),
                                offsideExempt).IsOffence(true)
                            : !offsideExempt && FootballDecisions.Offside(target.X, target.Yaw, ballX, direction,
                                agents.Where(a => a.Active && a.Team != possession).Select(a => (a.X, a.Yaw))));
                        if(physicalShots){physicalOffside.Clear();foreach(var candidate in agents.Where(a=>a.Active && a.Team==possession && a.Index!=carrier))if(OffsideSnapshot.Capture(candidate.X,candidate.Yaw,ballX,direction,agents.Where(a=>a.Active && a.Team!=possession).Select(a=>(a.X,a.Yaw)),offsideExempt).IsOffence(true))physicalOffside.Add(candidate.Index);}
                        headerCarrier = -1; offsideExempt = false;
                        bool throwing = throwCarrier==carrier;
                        bool cross = !throwing && target.Team == possession && (intelligence==null?target.X * direction > 18 && second - lastShot >= 150 && aerial.NextDouble() < (Math.Abs(owner.Z) > 8 ? .7 : .15):choice?.Kind is "cross_high" or "cross_low");
                        bool lofted=cross&&(intelligence==null||choice?.Kind!="cross_low");
                        Pose(owner, throwing ? "throw_in" : cross ? "cross" : "pass", second, second + 24);
                        owner.Yaw = Math.Atan2(target.X - owner.X, target.Z - owner.Z);
                        if (!physicalShots && lofted)
                        {
                            var rival = Nearest(1 - possession, target.X, target.Z);
                            if (Math.Sqrt(Math.Pow(rival.X - target.X, 2) + Math.Pow(rival.Z - target.Z, 2)) < 3)
                            {
                                Pose(rival, "air_duel", second + 12, second + 30);
                                if (aerial.NextDouble() < .35) { recipient = rival.Index; target = rival; receiverOffside = false; }
                            }
                        }
                        bool outOfPlay = !physicalShots && target.Team != possession && rules.NextDouble() < .15;
                        int passTicks = FlightTicks(Math.Sqrt(Math.Pow(target.X - ballX, 2) + Math.Pow(target.Z - ballZ, 2)), cross ? 15 : 19);
                        if (physicalShots && pendingPhysicalRestart==null) physicalContact = RecordContact(owner.Player.Id, possession, throwing ? "lateral" : cross ? "cruzamento" : "passe", second, ballX, ballY, ballZ, "jogada");
                        flight = new(ballX, ballZ, target.X, outOfPlay ? (ballZ >= 0 ? 35.2 : -35.2) : target.Z, second, second + passTicks, recipient, outOfPlay ? "throw_in" : "pass", carrier, possession, owner.Player, throwing ? "throw" : cross ? "cross" : "ground", throwing ? 1.95 : .24, cross ? 1.85 : .24, receiverOffside);
                        if(intelligence!=null&&choice!=null){if(target.Team==possession&&!outOfPlay)flight=flight with{TargetX=choice.X,TargetZ=choice.Z,Delivery=throwing?"throw":lofted?"cross":"ground",EndY=lofted?1.85:.24};decisionAt=second+passTicks+intelligence.DecisionDelay(recipient);if(intelligence.Announce(choice,owner.Player.Id,second))Event(second,"play_pattern",possession,owner.Player.Id,choice.Pattern+": "+owner.Player.Name+" procura "+target.Player.Name+".",target.Player.Id);}
                        if(throwing){flight=flight with{X=owner.X,Z=Math.Sign(owner.Z)*34.4};throwCarrier=-1;}
                        if(physicalShots && pendingPhysicalRestart==null)
                        {
                            var origin=new Position3(ballX,Math.Max(BallPhysics.Radius,ballY),ballZ);
                            double error=(rng.NextDouble()-rng.NextDouble())*(1-accuracy)*.7;
                            double dx=flight.TargetX-ballX,dz=flight.TargetZ-ballZ,distance=Math.Sqrt(dx*dx+dz*dz);
                            if(distance<.1){dx=Math.Sin(owner.Yaw);dz=Math.Cos(owner.Yaw);distance=1;}
                            var destination=new Position3(ballX+dx-dz*error,lofted?1.5:BallPhysics.Radius,ballZ+dz+dx*error);
                            physicalBall=lofted?BallDelivery.Launch(origin,destination,Math.Clamp(distance/20,.6,3),(rng.NextDouble()-.5)*10):BallDelivery.Ground(origin,destination,Math.Clamp(12+distance*.22,12,26));
                            touchingPlayers.Clear();touchingPlayers.Add(owner.Index);
                            flight=flight with{Result="physical",Delivery=lofted?"physical_cross":"physical_pass"};
                            Event(second,"pass_attempt",possession,owner.Player.Id,"Passe executado com impulso físico.",target.Player.Id);
                        }
                        if (physicalShots && pendingPhysicalRestart is BallRestart restartType)
                        {
                            var intended=agents.Where(a=>a.Active && a.Team==possession && a.Index!=carrier)
                                .OrderBy(a=>Math.Pow(a.X-flight.TargetX,2)+Math.Pow(a.Z-flight.TargetZ,2)).First();
                            double travel=restartType==BallRestart.Corner?1.6:restartType==BallRestart.ThrowIn?1.0:
                                teams[possession].Plan?.BuildShort==false?1.8:1.0;
                            double launchY=restartType==BallRestart.ThrowIn?1.95:BallPhysics.Radius;
                            double endHeight=restartType==BallRestart.Corner?1.4:.24;
                            physicalBall=BallDelivery.Launch(new(ballX,launchY,ballZ),new(intended.X,endHeight,intended.Z),travel);
                            touchingPlayers.Clear();
                            physicalContact=RecordContact(owner.Player.Id,possession,"cobrança",second,ballX,launchY,ballZ,restartType.ToString());
                            Event(second,"restart_taken",possession,owner.Player.Id,"Cobrança executada: "+restartType+".",intended.Player.Id);
                            flight=flight with {Result="physical",Start=second,Recipient=intended.Index,Delivery=restartType==BallRestart.Corner?"physical_cross":"physical_pass"};
                            physicalOffside.Clear(); // direct receipt from these restarts is exempt
                            pendingPhysicalRestart=null;
                            physicalRestartSpot=null;
                            throwCarrier=-1;
                        }
                    }
                }
            }
            if (captureFrames && (forceCapture || second % Tick == 0 || second == duration || events.Any(e => e.Second == second && e.Type is "substitution" or "red_card" or "injury" or "emergency_keeper"))) Capture(second);
        }
        events.Add(new((int)Math.Ceiling(duration / 60.0), "fulltime", setup.Home.Id, 0, "Fim de jogo.", duration));
        if(physicalShots && captureFrames && frames.Count>0)
            frames[^1]=frames[^1] with { Diagnostics=new ReplayDiagnostics("finished",null,events.Count-1) };
        var playerStats = teams.SelectMany((t, team) => t.Players.Concat(t.Bench ?? []).Where(p => secondsPlayed.ContainsKey(p.Id)).Select(p =>
        {
            var ev = events.Where(e => e.Player == p.Id && e.Team == t.Id).ToArray();
            return new MatchPlayerStats(p.Id, p.Name, t.Id, secondsPlayed[p.Id] / 60, t.Players.Any(x => x.Id == p.Id), ev.Count(e => e.Type == "goal"), ev.Count(e => e.Type is "goal" or "shot"), ev.Count(e => e.Type == "pass"), ev.Count(e => e.Type == "interception"), ev.Count(e => e.Type == "foul"), ev.Count(e => e.Type is "yellow_card" or "second_yellow"), ev.Count(e => e.Type == "red_card"));
        })).ToList();
        yield return new(duration, true, new(Version, duration, setup, events, frames, homeGoals, awayGoals, playerStats));
    }
}











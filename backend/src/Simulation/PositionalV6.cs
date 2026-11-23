using Checkpoint = Brasileirao.PositionalV5.Checkpoint;
namespace Brasileirao;

// O renderer nunca decide posições, posse, gols ou placar. O mesmo snapshot e seed
// geram a partida resumida e a reprodução. Frames são materializados apenas sob demanda.
public static class PositionalV6
{
    public const string Version = "positional-v6";
    public const int Duration = 90 * 60;
    // V1–V5 stay frozen. V6 adds grounded duels, delayed decisions and reviewed goals.
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
    static double ClampX(double x) => Math.Clamp(x, -51.5, 51.5);
    static double ClampZ(double z) => Math.Clamp(z, -32.5, 32.5);
    static double Round(double n) => Math.Round(n, 2);
    static double Rating(MatchPlayer p) => p.Rating * (.65 + .35 * p.Fitness / 100.0);

    public static MatchReplay Run(MatchSetup setup, int seed, bool captureFrames) => Stream(setup, seed, captureFrames).Last().Replay;
    public static IEnumerable<Checkpoint> Stream(MatchSetup setup, int seed, bool captureFrames = true)
    {
        if (setup.EngineVersion != Version) throw new GameRuleException("Versão do motor não suportada para esta reprodução.");
        if (setup.Home.Players.Count != 11 || setup.Away.Players.Count != 11) throw new GameRuleException("Uma partida exige duas equipes de onze jogadores.");
        Sports.ValidateTeam(setup.Home); Sports.ValidateTeam(setup.Away);
        int halfEnd = 2700, duration = Duration; bool penaltyKick = false, offsideExempt = false;
        var rules = new Random(seed ^ 0x73CA);
        var rng = new Random(seed);
        var aerial = new Random(seed ^ 0x6F17); var duels = new Random(seed ^ 0xD3E1);
        int lastDuel = -80;
        (int Until, int Team, double X, double Z, int Victim)? advantage = null;
        var discipline = new Random(seed ^ 0x51A7); var medical = new Random(seed ^ 0x371B);
        MatchTeam[] teams = [setup.Home, setup.Away];
        var slots = new[] { Tactics.Slots(teams[0].Formation, 1), Tactics.Slots(teams[1].Formation, -1) };
        var agents = teams.SelectMany((t, team) => t.Players.Select((p, slot) => new Agent(p, team, slot, slots[team][slot].X, slots[team][slot].Z))).ToArray();
        var unused = teams.Select(t => (t.Bench ?? []).ToList()).ToArray();
        int[] substitutions = [0, 0];
        HashSet<int>[] substitutionStops = [[], []]; var donePlans = new HashSet<(int, int)>(); var bookings = new Dictionary<int, int>(); var secondsPlayed = new Dictionary<int, int>();
        bool[] injuryOccurred = [false, false];
        List<ReplayFrame> frames = []; List<MatchEvent> events = [];
        int possession = 0, carrier = 6, decisionAt = 12, kickoffUntil = 0, homeGoals = 0, awayGoals = 0, lastShot = -150;
        double ballX = agents[carrier].X, ballZ = agents[carrier].Z, ballY = .24;
        Flight? flight = null;
        int headerCarrier = -1; bool forceCapture = false;
        void Pose(Agent a, string action, int second, int until) { a.ActionSecond = second; a.Action = action; a.ActionUntil = until; forceCapture = true; }

        void Event(int second, string type, int team, int player, string text, int? related = null, int injuryDays = 0) => events.Add(new(Math.Max(1, (int)Math.Ceiling(second / 60.0)), type, teams[team].Id, player, text, second, related, injuryDays));
        void Capture(int second)
        {
            if (!captureFrames || frames.Count > 0 && frames[^1].Second == second) return;
            frames.Add(new(second, agents.Select(a => new PlayerPose(a.Player.Id, a.Team, Round(a.X), Round(a.Z), Round(a.Yaw), a.Action, a.Active, a.Player.Name, a.Player.Position, a.ActionSecond)).ToList(), new(Round(ballX), Round(ballY), Round(ballZ))));
        }
        Agent Nearest(int team, double x, double z) => agents.Where(a => a.Active && a.Team == team && a.Slot > 0).MinBy(a => Math.Pow(a.X - x, 2) + Math.Pow(a.Z - z, 2))!;
        void EnsureCarrier() { if (!agents[carrier].Active) { carrier = Nearest(possession, ballX, ballZ).Index; decisionAt += 4; } }
        bool Replace(Agent outgoing, MatchPlayer incoming, int second, string reason)
        {
            if (second != halfEnd && !substitutionStops[outgoing.Team].Contains(second) && substitutionStops[outgoing.Team].Count >= 3) return false;
            if (substitutions[outgoing.Team] >= 5 || !outgoing.Active || !unused[outgoing.Team].Any(p => p.Id == incoming.Id)) return false;
            var original = outgoing.Player;
            if (outgoing.Slot == 0 && incoming.Position != "GOL") incoming = incoming with { NaturalPosition = incoming.Position, Position = "GOL", Rating = Math.Max(20, incoming.Rating / 2) };
            if (second != halfEnd) substitutionStops[outgoing.Team].Add(second);
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
                slots = [Tactics.Slots(teams[0].Formation, -1), Tactics.Slots(teams[1].Formation, 1)];
                foreach (var a in agents) { a.X = -a.X; a.Z = -a.Z; a.Yaw += Math.PI; a.VelocityX = a.VelocityZ = 0; }
                flight = null; headerCarrier = -1; offsideExempt = false; penaltyKick = false; possession = 1; carrier = 17; EnsureCarrier(); ballX = ballZ = 0; ballY = .24; kickoffUntil = second + 24; decisionAt = kickoffUntil;
                Event(second, "second_half", 0, 0, "Segundo tempo: as equipes trocam de lado.");
            }
            if (flight != null)
            {
                double progress = Math.Clamp((second - flight.Start) / (double)(flight.End - flight.Start), 0, 1);
                ballX = flight.X + (flight.TargetX - flight.X) * progress;
                ballZ = flight.Z + (flight.TargetZ - flight.Z) * progress;
                ballY = flight.StartY + (flight.EndY - flight.StartY) * progress + Math.Sin(progress * Math.PI) * (flight.Reviewed ? 0 : flight.Delivery == "cross" ? 1.3 : flight.Result == "pass" ? .3 : 1.3);
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
                        if(penalty){(halfEnd,duration)=MatchRules.EnsurePenaltyTime(second,halfEnd,duration);taker.X=((possession==0?1:-1)*(second>=halfEnd?-1:1))*41.5;taker.Z=0;lastShot=second-150;}
                        flight=null;kickoffUntil=second+12;decisionAt=kickoffUntil;Capture(second);continue;
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
                        foreach (var a in agents.Where(a => a.Active && a.Team == flight.Team)) { a.Action = "celebrate"; a.ActionUntil = second + 18; }
                        Capture(second);
                        possession = 1 - flight.Team; carrier = possession * 11 + 6; EnsureCarrier(); kickoffUntil = second + 24; decisionAt = kickoffUntil;
                        ballX = ballZ = 0; ballY = .24;
                    }
                    else if (flight.Result == "throw_in")
                    {
                        possession = 1 - flight.Team; carrier = Nearest(possession, flight.TargetX, Math.Clamp(flight.TargetZ, -33, 33)).Index;
                        agents[carrier].X = flight.TargetX; agents[carrier].Z = Math.Clamp(flight.TargetZ, -33, 33);
                        Event(second, "throw_in", possession, agents[carrier].Player.Id, "Bola pela lateral. Arremesso lateral.");
                        kickoffUntil = second + 8; decisionAt = kickoffUntil; offsideExempt = true;
                    }
                    else if (flight.Result is "save" or "miss")
                    {
                        var keeper = agents[(1 - flight.Team) * 11]; Pose(keeper, flight.Result == "save" ? "catch" : "keeper_ready", second, second + 6);
                        Event(second, "shot", flight.Team, flight.ShooterPlayer.Id, flight.Result == "save" ? "O goleiro faz a defesa." : "Finalização para fora.");
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
                        Event(second, possession == flight.Team ? "pass" : "interception", possession, possession == flight.Team ? flight.ShooterPlayer.Id : agents[carrier].Player.Id,
                            possession == flight.Team ? $"{flight.ShooterPlayer.Name} encontra {agents[carrier].Player.Name} no passe." : $"{agents[carrier].Player.Name} intercepta a jogada.", possession == flight.Team ? agents[carrier].Player.Id : null);
                        decisionAt = second + 12 + rng.Next(0, 7) * Tick;
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
                if (second % 30 == 0 && second < duration - 10 && second >= kickoffUntil && discipline.NextDouble() < (MatchRules.InPenaltyArea(agents[carrier].X, agents[carrier].Z, (possession == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1)) ? .004 : .14))
                {
                    var offender = Nearest(1 - possession, ballX, ballZ); var victim = agents[carrier];
                    if (Math.Pow(offender.X - victim.X, 2) + Math.Pow(offender.Z - victim.Z, 2) <= 2.75 * 2.75)
                    {
                        Event(second, "foul", offender.Team, offender.Player.Id, $"Falta de {offender.Player.Name} em {victim.Player.Name}.", victim.Player.Id);
                        var attackDirection = (possession == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1);
                        bool penaltyArea = MatchRules.InPenaltyArea(victim.X, victim.Z, attackDirection);
                        var severity = discipline.NextDouble();
                        bool dogso = victim.X * attackDirection > 30 && Math.Abs(victim.Z) < 14 && agents.Count(a => a.Active && a.Team == offender.Team && a.X * attackDirection > victim.X * attackDirection) <= 1;
                        bool attemptsBall = Math.Sqrt(Math.Pow(offender.X - victim.X, 2) + Math.Pow(offender.Z - victim.Z, 2)) < 2;
                        var sanction = FootballDecisions.Foul(severity > .87, severity > .998, dogso, penaltyArea, attemptsBall, victim.X * attackDirection > 18);
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
                        offsideExempt = false; penaltyKick = penalty; if (penalty) (halfEnd, duration) = MatchRules.EnsurePenaltyTime(second, halfEnd, duration);
                        if (penalty) { victim.X = ((possession == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1)) * 41.5; victim.Z = 0; lastShot = second - 150; }
                        kickoffUntil = second + (penalty ? 12 : 8); decisionAt = kickoffUntil;
                        }
                    }
                }
                if (second is 3600 or 4200 or 4800)
                    for (int team = 0; team < 2; team++)
                    {
                        if (!teams[team].AutoSubstitute || substitutions[team] >= 5) continue;
                        var tired = agents.Where(a => a.Active && a.Team == team && a.Slot > 0).OrderBy(a => a.Player.Fitness - secondsPlayed.GetValueOrDefault(a.Player.Id) / 60 * .20).FirstOrDefault(a => Reserve(a) != null);
                        if (tired != null && tired.Player.Fitness - secondsPlayed.GetValueOrDefault(tired.Player.Id) / 60 * .20 < 88) Replace(tired, Reserve(tired)!, second, "Troca automática para renovar o fôlego.");
                    }
            }
            if (advantage is { } pending)
            {
                if (possession != pending.Team)
                {
                    Event(second, "advantage_recalled", pending.Team, pending.Victim, "A vantagem não se concretiza. Falta original marcada.");
                    possession = pending.Team; carrier = Nearest(possession, pending.X, pending.Z).Index;
                    agents[carrier].X = pending.X; agents[carrier].Z = pending.Z;
                    flight = null; headerCarrier = -1; offsideExempt = false; penaltyKick = false; kickoffUntil = second + 12; decisionAt = kickoffUntil;
                    Event(second, "free_kick", pending.Team, pending.Victim, "Tiro livre direto no local da falta original.");
                    advantage = null;
                }
                else if (second >= pending.Until || flight?.Result is "goal" or "save" or "miss") advantage = null;
            }
            EnsureCarrier();
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
                var dx = ClampX(targetX) - a.X; var dz = ClampZ(targetZ) - a.Z;
                var distance = Math.Sqrt(dx * dx + dz * dz);
                var fatigue = Math.Clamp(1 - secondsPlayed.GetValueOrDefault(a.Player.Id) / 5400.0 * .16, .75, 1);
                var sprint = a.Index == carrier || a == nearestDefender || flight?.Recipient == a.Index;
                var topSpeed = (sprint ? 6.3 + Rating(a.Player) / 65 : 3.5 + Rating(a.Player) / 110) * fatigue;
                var physicalDelta = Tick / (double)TimeScale;
                var grounded = a.ActionUntil >= second && a.Action is "fall" or "tackle" or "air_duel";
                var striking = a.ActionUntil >= second && a.Action is "pass" or "shot" or "cross" or "header";
                var speed = Math.Min(grounded ? 0 : striking ? 1 : topSpeed, distance / physicalDelta);
                var desiredX = distance > .01 ? dx / distance * speed : 0;
                var desiredZ = distance > .01 ? dz / distance * speed : 0;
                var changeX = desiredX - a.VelocityX; var changeZ = desiredZ - a.VelocityZ;
                var change = Math.Sqrt(changeX * changeX + changeZ * changeZ);
                var blend = change > 0 ? Math.Min(1, 3.2 * physicalDelta / change) : 1;
                a.VelocityX += changeX * blend; a.VelocityZ += changeZ * blend;
                a.X = ClampX(a.X + a.VelocityX * physicalDelta); a.Z = ClampZ(a.Z + a.VelocityZ * physicalDelta);
                if (!striking && !grounded && Math.Abs(a.VelocityX) + Math.Abs(a.VelocityZ) > .05) a.Yaw = Math.Atan2(a.VelocityX, a.VelocityZ);
                if (a.ActionUntil < second)
                {
                    var action = a.Slot == 0 ? "keeper_ready" : a.Index == carrier && flight == null && second >= kickoffUntil ? "dribble" : distance > .5 ? "run" : "idle";
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
            if (flight == null && second >= kickoffUntil && second - lastDuel >= 30 && agents[carrier].Slot > 0)
            {
                var holder = agents[carrier]; var challenger = Nearest(1 - possession, holder.X, holder.Z);
                var separation = Math.Sqrt(Math.Pow(holder.X - challenger.X, 2) + Math.Pow(holder.Z - challenger.Z, 2));
                if (separation < 1.7 && challenger.ActionUntil < second && holder.Action is not "fall" && duels.NextDouble() < .4)
                {
                    lastDuel = second;
                    var facing = Math.Atan2(holder.X - challenger.X, holder.Z - challenger.Z);
                    challenger.Yaw = facing;
                    var chance = Math.Clamp(.42 + (Rating(challenger.Player) - Rating(holder.Player)) / 180, .2, .7);
                    bool sliding = duels.NextDouble() < .3;
                    Pose(challenger, sliding ? "tackle" : "challenge", second, second + (sliding ? 26 : 14));
                    if (duels.NextDouble() < chance)
                    {
                        Event(second, "tackle", challenger.Team, challenger.Player.Id, $"{challenger.Player.Name} chega na bola e desarma {holder.Player.Name}.", holder.Player.Id);
                        Pose(holder, sliding ? "fall" : "stumble", second, second + (sliding ? 36 : 18));
                        possession = challenger.Team; carrier = challenger.Index; decisionAt = second + 30;
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
                if (second >= decisionAt && second < duration - 8 && !(owner.ActionUntil >= second && owner.Action is "tackle" or "fall"))
                {
                    double direction = (possession == 0 ? 1 : -1) * (second >= halfEnd ? -1 : 1);
                    var ownRating = agents.Where(a => a.Active && a.Team == possession).Average(a => Rating(a.Player));
                    var otherRating = agents.Where(a => a.Active && a.Team != possession).Average(a => Rating(a.Player));
                    if (owner.Slot > 0 && (penaltyKick || headerCarrier == carrier || owner.X * direction > 24 && Math.Abs(owner.Z) <= 8 && second - lastShot >= 150))
                    {
                        var mentality = teams[possession].Mentality == "Ofensiva" ? .035 : teams[possession].Mentality == "Defensiva" ? -.025 : 0;
                        var defensiveBonus = teams[1 - possession].Mentality == "Defensiva" ? .035 : 0;
                        var probability = Math.Clamp(.09 + (ownRating - otherRating) / 380 + mentality - defensiveBonus + (60 - Rating(agents[(1 - possession) * 11].Player)) / 600, .035, .26);
                        if (penaltyKick) probability = .76;
                        lastShot = second; penaltyKick = false; offsideExempt = false;
                        var result = rng.NextDouble() < probability ? "goal" : rng.NextDouble() < .7 ? "save" : "miss";
                        bool header = headerCarrier == carrier; headerCarrier = -1;
                        Pose(owner, header ? "header" : "shot", second, second + 18);
                        var keeper = agents[(1 - possession) * 11];
                        double targetX = direction * (result == "goal" ? 54 : 51), targetZ = result == "miss" ? 6 : rng.NextDouble() * 5 - 2.5;
                        if (result == "save") { targetX = keeper.X - direction * .45; targetZ = Math.Clamp(targetZ, keeper.Z - 1.35, keeper.Z + 1.35); }
                        int shotTicks = FlightTicks(Math.Sqrt(Math.Pow(targetX - owner.X, 2) + Math.Pow(targetZ - owner.Z, 2)), header ? 18 : 28);
                        var lateral = (targetX - keeper.X) * Math.Cos(keeper.Yaw) - (targetZ - keeper.Z) * Math.Sin(keeper.Yaw);
                        Pose(keeper, result == "miss" ? "keeper_ready" : Math.Abs(lateral) < .5 ? "catch" : lateral > 0 ? "dive_left" : "dive_right", second + shotTicks, second + shotTicks + 12);
                        ballX = owner.X + direction * (header ? .12 : .5); ballY = header ? 1.85 : .24;
                        owner.Yaw = Math.Atan2(targetX - owner.X, targetZ - owner.Z);
                        var obstruction = agents.Any(a => a.Active && a.Team == possession && a.Index != carrier && FootballDecisions.Offside(a.X, a.Yaw, ballX, direction, agents.Where(d => d.Active && d.Team != possession).Select(d => (d.X, d.Yaw))) && RefereeDecisions.ObstructsView(a.X, a.Z, keeper.X, keeper.Z, ballX, ballZ));
                        flight = new(ballX, ballZ, targetX, targetZ, second, second + shotTicks, (1 - possession) * 11, result, carrier, possession, owner.Player, header ? "header" : "ground", ballY, result == "save" ? 1.4 : header ? 1.0 : .24, false, false, obstruction ? "offside_interference" : "");
                    }
                    else
                    {
                        var candidates = agents.Select((a, i) => (Agent: a, Index: i)).Where(a => a.Agent.Active && a.Agent.Team == possession && a.Index != carrier && a.Agent.Slot > 0)
                            .OrderByDescending(a => a.Agent.X * direction + rng.NextDouble() * 12 - Math.Sqrt(Math.Pow(a.Agent.X - owner.X, 2) + Math.Pow(a.Agent.Z - owner.Z, 2)) * .22
                                + agents.Where(d => d.Active && d.Team != possession).Min(d => Math.Sqrt(Math.Pow(d.X - a.Agent.X, 2) + Math.Pow(d.Z - a.Agent.Z, 2))) * .7).Take(4).ToArray();
                        var recipient = candidates[rng.Next(candidates.Length)].Index;
                        var receiver = agents[recipient];
                        var laneDefender = agents.Where(d => d.Active && d.Team != possession && d.Slot > 0).MinBy(d => FootballDecisions.PassingLaneDistance(d.X, d.Z, owner.X, owner.Z, receiver.X, receiver.Z))!;
                        var laneDistance = FootballDecisions.PassingLaneDistance(laneDefender.X, laneDefender.Z, owner.X, owner.Z, receiver.X, receiver.Z);
                        var accuracy = Math.Clamp(.84 + (ownRating - otherRating) / 500 - Math.Max(0, 3 - laneDistance) * .04, .65, .94);
                        if (rng.NextDouble() > accuracy) recipient = laneDefender.Index;
                        var target = agents[recipient];
                        bool receiverOffside = !offsideExempt && target.Team == possession && FootballDecisions.Offside(target.X, target.Yaw, ballX, direction, agents.Where(a => a.Active && a.Team != possession).Select(a => (a.X, a.Yaw)));
                        headerCarrier = -1; offsideExempt = false;
                        bool cross = target.Team == possession && target.X * direction > 18 && second - lastShot >= 150 && aerial.NextDouble() < (Math.Abs(owner.Z) > 8 ? .7 : .15);
                        Pose(owner, cross ? "cross" : "pass", second, second + 18);
                        owner.Yaw = Math.Atan2(target.X - owner.X, target.Z - owner.Z);
                        if (cross)
                        {
                            var rival = Nearest(1 - possession, target.X, target.Z);
                            if (Math.Sqrt(Math.Pow(rival.X - target.X, 2) + Math.Pow(rival.Z - target.Z, 2)) < 3)
                            {
                                Pose(rival, "air_duel", second + 12, second + 30);
                                if (aerial.NextDouble() < .35) { recipient = rival.Index; target = rival; receiverOffside = false; }
                            }
                        }
                        bool outOfPlay = target.Team != possession && rules.NextDouble() < .15;
                        int passTicks = FlightTicks(Math.Sqrt(Math.Pow(target.X - ballX, 2) + Math.Pow(target.Z - ballZ, 2)), cross ? 15 : 19);
                        flight = new(ballX, ballZ, target.X, outOfPlay ? (ballZ >= 0 ? 34 : -34) : target.Z, second, second + passTicks, recipient, outOfPlay ? "throw_in" : "pass", carrier, possession, owner.Player, cross ? "cross" : "ground", .24, cross ? 1.85 : .24, receiverOffside);
                    }
                }
            }
            if (captureFrames && (forceCapture || second % Tick == 0 || second == duration || events.Any(e => e.Second == second && e.Type is "substitution" or "red_card" or "injury" or "emergency_keeper"))) Capture(second);
        }
        events.Add(new((int)Math.Ceiling(duration / 60.0), "fulltime", setup.Home.Id, 0, "Fim de jogo.", duration));
        var playerStats = teams.SelectMany((t, team) => t.Players.Concat(t.Bench ?? []).Where(p => secondsPlayed.ContainsKey(p.Id)).Select(p =>
        {
            var ev = events.Where(e => e.Player == p.Id && e.Team == t.Id).ToArray();
            return new MatchPlayerStats(p.Id, p.Name, t.Id, secondsPlayed[p.Id] / 60, t.Players.Any(x => x.Id == p.Id), ev.Count(e => e.Type == "goal"), ev.Count(e => e.Type is "goal" or "shot"), ev.Count(e => e.Type == "pass"), ev.Count(e => e.Type == "interception"), ev.Count(e => e.Type == "foul"), ev.Count(e => e.Type is "yellow_card" or "second_yellow"), ev.Count(e => e.Type == "red_card"));
        })).ToList();
        yield return new(duration, true, new(Version, duration, setup, events, frames, homeGoals, awayGoals, playerStats));
    }
}


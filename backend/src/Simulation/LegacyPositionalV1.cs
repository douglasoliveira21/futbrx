namespace Brasileirao;

// O renderer nunca decide posições, posse, gols ou placar. O mesmo snapshot e seed
// geram a partida resumida e a reprodução. Frames são materializados apenas sob demanda.
public static class LegacyPositionalV1
{
    public const string Version = "positional-v1";
    public const int Duration = 90 * 60;
    const int Tick = 2;
    sealed class Agent(MatchPlayer player, int team, int slot, double x, double z)
    {
        public MatchPlayer Player = player;
        public int Team = team, Slot = slot;
        public int Index => Team * 11 + Slot;
        public double X = x, Z = z, Yaw = team == 0 ? Math.PI / 2 : -Math.PI / 2;
        public string Action = "idle";
        public int ActionUntil;
    }
    sealed record Flight(double X, double Z, double TargetX, double TargetZ, int Start, int End, int Recipient, string Result, int Shooter, int Team);
    static double ClampX(double x) => Math.Clamp(x, -51.5, 51.5);
    static double ClampZ(double z) => Math.Clamp(z, -32.5, 32.5);
    static double Round(double n) => Math.Round(n, 2);
    static double Rating(MatchPlayer p) => p.Rating * (.65 + .35 * p.Fitness / 100.0);

    public static MatchReplay Run(MatchSetup setup, int seed, bool captureFrames)
    {
        if (setup.EngineVersion != Version) throw new GameRuleException("Versão do motor não suportada para esta reprodução.");
        if (setup.Home.Players.Count != 11 || setup.Away.Players.Count != 11) throw new GameRuleException("Uma partida exige duas equipes de onze jogadores.");
        var rng = new Random(seed);
        MatchTeam[] teams = [setup.Home, setup.Away];
        var slots = new[] { Tactics.Slots(teams[0].Formation, 1), Tactics.Slots(teams[1].Formation, -1) };
        var agents = teams.SelectMany((t, team) => t.Players.Select((p, slot) => new Agent(p, team, slot, slots[team][slot].X, slots[team][slot].Z))).ToArray();
        List<ReplayFrame> frames = []; List<MatchEvent> events = [];
        int possession = 0, carrier = 6, decisionAt = 12, kickoffUntil = 0, homeGoals = 0, awayGoals = 0, lastShot = -150;
        double ballX = agents[carrier].X, ballZ = agents[carrier].Z, ballY = .24;
        Flight? flight = null;
        void Event(int second, string type, int team, int player, string text) => events.Add(new(Math.Max(1, (int)Math.Ceiling(second / 60.0)), type, teams[team].Id, player, text, second));
        void Capture(int second)
        {
            if (!captureFrames || frames.Count > 0 && frames[^1].Second == second) return;
            frames.Add(new(second, agents.Select(a => new PlayerPose(a.Player.Id, a.Team, Round(a.X), Round(a.Z), Round(a.Yaw), a.Action)).ToList(), new(Round(ballX), Round(ballY), Round(ballZ))));
        }
        Capture(0);
        for (int second = Tick; second <= Duration; second += Tick)
        {
            if (flight != null)
            {
                double progress = Math.Clamp((second - flight.Start) / (double)(flight.End - flight.Start), 0, 1);
                ballX = flight.X + (flight.TargetX - flight.X) * progress;
                ballZ = flight.Z + (flight.TargetZ - flight.Z) * progress;
                ballY = .24 + Math.Sin(progress * Math.PI) * (flight.Result == "pass" ? .3 : 2.2);
                if (second >= flight.End)
                {
                    if (flight.Result == "goal")
                    {
                        if (flight.Team == 0) homeGoals++; else awayGoals++;
                        Event(second, "goal", flight.Team, agents[flight.Shooter].Player.Id, $"Gol de {agents[flight.Shooter].Player.Name}!");
                        foreach (var a in agents.Where(a => a.Team == flight.Team)) { a.Action = "celebrate"; a.ActionUntil = second + 18; }
                        Capture(second);
                        possession = 1 - flight.Team; carrier = possession * 11 + 6; kickoffUntil = second + 24; decisionAt = kickoffUntil;
                        ballX = ballZ = 0; ballY = .24;
                    }
                    else if (flight.Result is "save" or "miss")
                    {
                        var keeper = agents[(1 - flight.Team) * 11]; keeper.Action = "save"; keeper.ActionUntil = second + 8;
                        Event(second, "shot", flight.Team, agents[flight.Shooter].Player.Id, flight.Result == "save" ? "O goleiro faz a defesa." : "Finalização para fora.");
                        possession = 1 - flight.Team; carrier = possession * 11; decisionAt = second + 10;
                    }
                    else
                    {
                        carrier = flight.Recipient; possession = agents[carrier].Team;
                        Event(second, possession == flight.Team ? "pass" : "interception", possession, agents[carrier].Player.Id,
                            possession == flight.Team ? $"{agents[carrier].Player.Name} recebe o passe." : $"{agents[carrier].Player.Name} intercepta a jogada.");
                        decisionAt = second + 12 + rng.Next(0, 7) * Tick;
                    }
                    flight = null;
                }
            }
            // Forma coletiva, pressão ao portador e ocupação de espaço são calculadas no servidor.
            var nearestDefender = agents.Where(a => a.Team != possession && a.Slot > 0).MinBy(a => Math.Pow(a.X - ballX, 2) + Math.Pow(a.Z - ballZ, 2))!;
            foreach (var a in agents)
            {
                double direction = a.Team == 0 ? 1 : -1;
                var slot = slots[a.Team][a.Slot];
                double push = teams[a.Team].Mentality == "Ofensiva" ? 8 : teams[a.Team].Mentality == "Defensiva" ? -6 : 0;
                double targetX = a.Slot == 0 ? direction * -49 : slot.X + ballX * .34 + direction * (a.Team == possession ? 5 + push : -3);
                double targetZ = a.Slot == 0 ? ballZ * .12 : slot.Z + ballZ * .24;
                if (kickoffUntil > second) { targetX = slot.X; targetZ = slot.Z; }
                else if (a == agents[carrier] && flight == null) { targetX = a.X + direction * 10; targetZ = a.Z * .92; }
                else if (a == nearestDefender) { targetX = ballX - direction * 2; targetZ = ballZ; }
                else if (flight?.Recipient == a.Index) { targetX = flight.TargetX; targetZ = flight.TargetZ; }
                var dx = ClampX(targetX) - a.X; var dz = ClampZ(targetZ) - a.Z;
                var distance = Math.Sqrt(dx * dx + dz * dz);
                // Escala temporal conservadora: deslocamento entre ticks é limitado e interpolável.
                double step = Math.Min(distance, Tick * (1.0 + Rating(a.Player) / 100));
                if (distance > .1) { a.X += dx / distance * step; a.Z += dz / distance * step; a.Yaw = Math.Atan2(dx, dz); }
                if (a.ActionUntil < second) a.Action = distance > .5 ? "run" : "idle";
            }
            if (flight == null && second >= kickoffUntil)
            {
                var owner = agents[carrier]; ballX = owner.X; ballZ = owner.Z; ballY = .24;
                if (second >= decisionAt && second < Duration - 8)
                {
                    double direction = possession == 0 ? 1 : -1;
                    var ownRating = teams[possession].Players.Average(Rating);
                    var otherRating = teams[1 - possession].Players.Average(Rating);
                    if (owner.X * direction > 24 && owner.Slot > 0 && second - lastShot >= 150)
                    {
                        var mentality = teams[possession].Mentality == "Ofensiva" ? .035 : teams[possession].Mentality == "Defensiva" ? -.025 : 0;
                        var defensiveBonus = teams[1 - possession].Mentality == "Defensiva" ? .035 : 0;
                        var probability = Math.Clamp(.13 + (ownRating - otherRating) / 220 + mentality - defensiveBonus, .04, .35);
                        lastShot = second;
                        var result = rng.NextDouble() < probability ? "goal" : rng.NextDouble() < .7 ? "save" : "miss";
                        owner.Action = "kick"; owner.ActionUntil = second + 6;
                        flight = new(ballX, ballZ, direction * (result == "goal" ? 54 : 51), result == "miss" ? 6 : rng.NextDouble() * 5 - 2.5, second, second + 6, (1 - possession) * 11, result, carrier, possession);
                    }
                    else
                    {
                        var candidates = agents.Select((a, i) => (Agent: a, Index: i)).Where(a => a.Agent.Team == possession && a.Index != carrier && a.Agent.Slot > 0)
                            .OrderByDescending(a => a.Agent.X * direction + rng.NextDouble() * 30).Take(4).ToArray();
                        var recipient = candidates[rng.Next(candidates.Length)].Index;
                        if (rng.NextDouble() > Math.Clamp(.82 + (ownRating - otherRating) / 250, .55, .94)) recipient = nearestDefender.Index;
                        var target = agents[recipient];
                        owner.Action = "kick"; owner.ActionUntil = second + 4;
                        flight = new(ballX, ballZ, target.X, target.Z, second, second + 6, recipient, "pass", carrier, possession);
                    }
                }
            }
            if (second % 6 == 0 || second == Duration) Capture(second);
        }
        events.Add(new(90, "fulltime", setup.Home.Id, 0, "Fim de jogo.", Duration));
        return new(Version, Duration, setup, events, frames, homeGoals, awayGoals);
    }
}


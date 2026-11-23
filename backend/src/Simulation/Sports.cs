namespace Brasileirao;

public static class Sports
{
    public static bool Eligible(Player p) => p.InjuryDays == 0 && p.SuspensionMatches == 0;
    public static string Reason(Player p) => p.InjuryDays > 0 ? $"{p.InjuryName}: {p.InjuryDays} dias de recuperação" : p.SuspensionMatches > 0 ? $"Suspenso por {p.SuspensionMatches} partida(s)" : "Disponível";
    public static void Upgrade(GameState g)
    {
        if (g.SportsVersion >= 1) return;
        g.PlayerStatsFromRound = g.Round; g.SportsVersion = 1; RefreshBench(g);
    }
    public static void RepairSelection(List<Player> squad, bool preserve)
    {
        var available = squad.Where(Eligible).ToList();
        var chosen = preserve ? available.Where(p => p.Starter).Take(11).ToList() : [];
        var keeper = chosen.FirstOrDefault(p => p.Position == "GOL") ?? available.Where(p => p.Position == "GOL").OrderByDescending(Strength).FirstOrDefault();
        chosen.RemoveAll(p => p.Position == "GOL" && p != keeper);
        if (keeper != null && !chosen.Contains(keeper)) { if (chosen.Count == 11) chosen.RemoveAt(chosen.Count - 1); chosen.Insert(0, keeper); }
        chosen.AddRange(available.Where(p => p.Position != "GOL" && !chosen.Contains(p)).OrderByDescending(Strength).Take(11 - chosen.Count));
        foreach (var p in squad) p.Starter = chosen.Contains(p);
    }
    static double Strength(Player p) => p.Rating * .7 + p.Fitness * .3;
    public static void RefreshBench(GameState g)
    {
        if(g.TacticalPlan.Roles!=null)g.TacticalPlan=g.TacticalPlan with{Roles=g.TacticalPlan.Roles.Where(r=>g.Squad.Any(p=>p.Id==r.Key)).ToDictionary()};
        if(g.TacticalPlan.Positions!=null)g.TacticalPlan=g.TacticalPlan with{Positions=g.TacticalPlan.Positions.Where(r=>g.Squad.Any(p=>p.Id==r.Key)).ToDictionary()};
        var eligible = g.Squad.Where(p => !p.Starter && Eligible(p)).ToList();
        g.BenchIds = g.BenchIds.Where(id => eligible.Any(p => p.Id == id)).Distinct().Take(12).ToList();
        g.BenchIds.AddRange(eligible.Where(p => !g.BenchIds.Contains(p.Id)).OrderByDescending(p => p.Position == "GOL").ThenByDescending(Strength).Take(12 - g.BenchIds.Count).Select(p => p.Id));
    }
    public static void ValidateTeam(MatchTeam team)
    {
        if (team.Players.Count != 11 || team.Players.Count(p => p.Position == "GOL") != 1 || team.Players.Any(p => p.Fitness < 1)) throw new GameRuleException("A escalação exige onze titulares aptos e um goleiro.");
        var bench = team.Bench ?? []; var plans = team.Substitutions ?? [];
        if (bench.Count > 12 || team.Players.Concat(bench).Select(p => p.Id).Distinct().Count() != team.Players.Count + bench.Count) throw new GameRuleException("Banco deve conter até doze reservas distintos dos titulares.");
        if (plans.Any(p => p == null) || plans.Count > 5 || plans.Select(p => p.OutPlayerId).Distinct().Count() != plans.Count || plans.Select(p => p.InPlayerId).Distinct().Count() != plans.Count) throw new GameRuleException("Programe até cinco trocas com jogadores de saída e entrada distintos.");
        foreach (var plan in plans)
        {
            var outgoing = team.Players.Find(p => p.Id == plan.OutPlayerId); var incoming = bench.Find(p => p.Id == plan.InPlayerId);
            if (plan.Minute is < 1 or > 89 || outgoing == null || incoming == null || (outgoing.Position == "GOL") != (incoming.Position == "GOL")) throw new GameRuleException("Confira minuto (1–89), titular de saída e reserva de entrada. Trocas de goleiro exigem outro goleiro.");
        }
    }
    public static void Swap(GameState g, int outgoingId, int incomingId)
    {
        var outgoing = g.Squad.Find(p => p.Id == outgoingId); var incoming = g.Squad.Find(p => p.Id == incomingId);
        if (outgoing == null || incoming == null || !outgoing.Starter || incoming.Starter || !Eligible(incoming)) throw new GameRuleException("Escolha um titular e um reserva disponível do seu elenco.");
        if ((outgoing.Position == "GOL") != (incoming.Position == "GOL")) throw new GameRuleException("Troque o goleiro por outro goleiro.");
        outgoing.Starter = false; incoming.Starter = true; g.Substitutions.Clear(); RefreshBench(g);
        // The incoming player takes over the tactical slot and individual role of the one who left,
        // as a substitution during the match does.
        var plan = g.TacticalPlan;
        if (plan.Positions?.TryGetValue(outgoingId, out var point) == true)
        {
            var positions = new Dictionary<int, TacticalPoint>(plan.Positions); positions.Remove(outgoingId); positions[incomingId] = point;
            plan = plan with { Positions = positions };
        }
        if (plan.Roles?.TryGetValue(outgoingId, out var role) == true)
        {
            var roles = new Dictionary<int, string>(plan.Roles); roles.Remove(outgoingId); roles[incomingId] = role;
            plan = plan with { Roles = roles };
        }
        g.TacticalPlan = plan;
    }
    public static MatchTeam BuildTeam(GameState g, string club)
    {
        bool managed = club == g.ClubId && CoachCareer.IsEmployed(g); var squad = club==g.ClubId ? g.Squad : g.OpponentSquads[club];
        if (!managed) RepairSelection(squad, false);
        var players = squad.Where(p => p.Starter && Eligible(p)).OrderBy(p => Tactics.RoleOrder(p.Position)).ThenBy(p => p.Id).Select(p=>ToMatch(g,p,managed)).ToList();
        if (managed && players.Count != 11) throw new GameRuleException("Escale onze titulares disponíveis. Use a escalação automática ou substitua os atletas lesionados/suspensos.");
        if (!managed && players.Count < 11)
        {
            // Convocações temporárias da base evitam que uma equipe da IA trave a rodada.
            int clubIndex = g.Clubs.FindIndex(c => c.Id == club);
            for (int i = players.Count; i < 11; i++) players.Add(new(-10000 - clubIndex * 100 - i, $"Reserva da base {i + 1}", "MEI", 45, 100));
        }
        if (players.Count(p => p.Position == "GOL") == 0)
        {
            var first = players[0]; players[0] = first with { Position = "GOL", NaturalPosition = first.Position, Rating = Math.Max(20, first.Rating / 2) };
        }
        var bench = managed ? g.BenchIds.Select(id => squad.Find(p => p.Id == id)).Where(p => p != null && !p.Starter && Eligible(p)).Select(p => ToMatch(g,p!,managed)).ToList() : squad.Where(p => !p.Starter && Eligible(p)).OrderByDescending(p => p.Position == "GOL").ThenByDescending(Strength).Take(12).Select(p=>ToMatch(g,p,managed)).ToList();
        var coach=CoachCareer.At(g,club);
        var plan=managed?g.TacticalPlan:coach?.Style=="Defensiva"?new TacticalPlan("Contra-ataque","Baixa","Fechada",false):coach?.Style=="Ofensiva"?new TacticalPlan("Equilibrado","Alta","Aberta",true,true):new TacticalPlan();
        var team = new MatchTeam(club, managed ? g.Formation : coach?.Formation??"4-3-3", managed ? g.Mentality : coach?.Style??"Equilibrada", players, bench, managed ? [.. g.Substitutions] : [], managed ? g.AutoSubstitute : true,plan,!managed);
        ValidateTeam(team); return team;
    }
    static MatchPlayer ToMatch(GameState g,Player p,bool managed) => new(p.Id, p.Name, p.Position, Math.Clamp(p.Rating+(managed?ClubLife.MoraleBonus(g,p)+ClubLife.Quality(g,"Analista")/40:0),1,99), p.Fitness,Skills:g.Development.TryGetValue(p.Id,out var skills)?new(skills.Attributes):null,PreferredFoot:p.PreferredFoot);
    public static void Prepare(GameState g)
    {
        Upgrade(g);
        if (g.AutoReplaceUnavailable) RepairSelection(g.Squad, true);
        RefreshBench(g);
        BuildTeam(g, g.ClubId);
    }
    public static void SavePlan(GameState g, List<SubstitutionPlan> plans, bool automatic, bool replaceUnavailable)
    {
        if (plans == null || plans.Any(p => p == null) || plans.Count > 5) throw new GameRuleException("Informe até cinco substituições programadas.");
        var original = g.Substitutions; var oldAuto = g.AutoSubstitute; var oldReplace = g.AutoReplaceUnavailable;
        g.Substitutions = plans.OrderBy(p => p.Minute).ToList(); g.AutoSubstitute = automatic; g.AutoReplaceUnavailable = replaceUnavailable;
        try { RefreshBench(g); BuildTeam(g, g.ClubId); } catch { g.Substitutions = original; g.AutoSubstitute = oldAuto; g.AutoReplaceUnavailable = oldReplace; throw; }
    }
    public static void AfterRound(GameState g,List<Fixture> fixtures,bool onlyParticipants=false,int recoveryDays=7)=>AfterRound(g,fixtures,onlyParticipants,recoveryDays,true);
    public static void AfterRound(GameState g, List<Fixture> fixtures, bool onlyParticipants,int recoveryDays,bool accumulateStatistics)
    {
        var summaries = fixtures.SelectMany(f => f.PlayerStats).ToList();
        var minutes = summaries.GroupBy(s => s.PlayerId).ToDictionary(s => s.Key, s => s.Sum(p => p.Minutes));
        var participating = fixtures.SelectMany(f => new[] { f.Home, f.Away }).ToHashSet();
        var registered = g.Squad.Where(p => !onlyParticipants || participating.Contains(g.ClubId)).Concat(g.OpponentSquads.Where(k => !onlyParticipants || participating.Contains(k.Key)).SelectMany(k => k.Value)).ToList();
        foreach (var p in registered.Concat(onlyParticipants ? [] : g.Market).Concat(onlyParticipants ? [] : g.Academy))
        {
            int oldInjury = p.InjuryDays, oldSuspension = p.SuspensionMatches;
            p.InjuryDays = Math.Max(0, p.InjuryDays - recoveryDays); if (p.InjuryDays == 0) p.InjuryName = "";
            if (registered.Contains(p)) p.SuspensionMatches = Math.Max(0, p.SuspensionMatches - 1);
            int played = minutes.GetValueOrDefault(p.Id);
            double recovery = (played == 0 ? 18 : 12)*recoveryDays/7.0;
            p.Fitness = (int)Math.Clamp(Math.Round(p.Fitness + recovery - played * .16), 25, 100);
            if (g.Squad.Contains(p) && (oldInjury > 0 || oldSuspension > 0) && Eligible(p)) g.News.Insert(0, $"{p.Name} está novamente disponível para a equipe.");
        }
        foreach (var fixture in fixtures)
        {
            foreach (var group in fixture.Events.Where(e => e.Type is "injury" or "yellow_card" or "second_yellow" or "red_card").GroupBy(e => e.Player))
            {
                var p = registered.Find(p => p.Id == group.Key); if (p == null) continue;
                int newInjury = group.Where(e => e.Type == "injury").Select(e => e.InjuryDays).DefaultIfEmpty(0).Max();
                if (newInjury > 0) { p.InjuryDays = Math.Max(p.InjuryDays, newInjury); p.InjuryName = "Desconforto muscular"; }
                int yellow = group.Count(e => e.Type is "yellow_card" or "second_yellow"); p.YellowCards += yellow;
                bool accumulation = p.YellowCards >= 3; p.YellowCards %= 3;
                if (accumulation || group.Any(e => e.Type == "red_card")) p.SuspensionMatches++;
                if (g.Squad.Contains(p) && newInjury > 0) g.News.Insert(0, $"Departamento médico: {p.Name} ficará fora por cerca de {p.InjuryDays} dias.");
                if (g.Squad.Contains(p) && (accumulation || group.Any(e => e.Type == "red_card"))) g.News.Insert(0, $"Disciplina: {p.Name} cumprirá suspensão na próxima partida.");
            }
        }
        foreach (var s in summaries.Where(_=>accumulateStatistics))
        {
            if (!g.PlayerStats.TryGetValue(s.PlayerId, out var stats)) g.PlayerStats[s.PlayerId] = stats = new() { PlayerId = s.PlayerId };
            stats.PlayerName = s.PlayerName; stats.ClubId = s.ClubId; stats.Appearances++; stats.Starts += s.Started ? 1 : 0; stats.Minutes += s.Minutes; stats.Goals += s.Goals; stats.Shots += s.Shots; stats.Passes += s.Passes; stats.Interceptions += s.Interceptions; stats.Fouls += s.Fouls; stats.YellowCards += s.YellowCards; stats.RedCards += s.RedCards;
        }
        g.Substitutions.Clear();
        if (g.AutoReplaceUnavailable) RepairSelection(g.Squad, true);
        RefreshBench(g);
    }
}

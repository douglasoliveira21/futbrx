using Brasileirao;

// Two-legged knockout ties: legs, aggregate winner, shootout only when the aggregate is level.
public static class KnockoutLegsTests
{
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }
    static bool Throws(Action action) { try { action(); return false; } catch (GameRuleException) { return true; } }
    static void Score(Fixture f, int home, int away, int? homePenalties = null, int? awayPenalties = null)
    {
        f.HomeGoals = home; f.AwayGoals = away; f.HomePenalties = homePenalties; f.AwayPenalties = awayPenalties;
    }
    // A career with no competitions of its own, so only the ones created here exist.
    static GameState Career()
    {
        var g = Game.New("pal");
        g.Life!.Competitions.Clear();
        return g;
    }
    static List<string> Others(GameState g, int count) => g.Clubs.Where(c => c.Id != g.ClubId).Take(count).Select(c => c.Id).ToList();

    public static void Run()
    {
        // The default competitions keep one match per tie; the state circuit is now home and away.
        var fresh = Game.New("pal");
        var state = fresh.Life!.Competitions.Single(c => c.Id == "state");
        var cup = fresh.Life.Competitions.Single(c => c.Id == "copa");
        Check(state.Legs == 2 && state.Matches.Count == 4 && state.Matches.Count(m => m.Leg == 2) == 2, "Estadual demonstrativo tem duas partidas por confronto");
        Check(cup.Legs == 1 && cup.Matches.All(m => m.Leg == 1 && m.Fixture.Leg == 0), "Copa do Brasil segue com jogo único");

        // Creation: first leg at the stage round, second leg a week later with the home advantage swapped.
        var g = Career();
        var two = ClubCompetitions.Add(g, "ida-volta", "Mata-mata de teste", Others(g, 8), [3, 8, 12], 0, 2);
        var first = two.Matches.Where(m => m.Leg == 1).ToList();
        Check(two.Matches.Count == 8 && first.Count == 4, "Quatro confrontos geram oito partidas");
        Check(first.All(l1 => two.Matches.Single(m => m.Tie == l1.Tie && m.Leg == 2) is var l2 && l2.Fixture.Home == l1.Fixture.Away && l2.Fixture.Away == l1.Fixture.Home && l1.Fixture.Round == 3 && l2.Fixture.Round == 4), "Volta é na semana seguinte e com o mando invertido");
        Check(two.Matches.All(m => m.Fixture.Leg == m.Leg && m.Fixture.CompetitionName == "Mata-mata de teste") && two.Matches.Select(m => m.Id).Distinct().Count() == 8, "Partidas identificam perna, competição e id próprio");
        Check(two.Matches.Single(m => m.Leg == 2 && m.Tie == first[0].Tie).Fixture.Date > first[0].Fixture.Date, "Volta acontece depois da ida");
        Check(two.Format.Contains("ida e volta") && !Throws(() => ClubCompetitions.Setup(g, two, first[0])), "Formato descreve a ida e volta");
        Check(Throws(() => ClubCompetitions.Setup(g, two, two.Matches.Single(m => m.Leg == 2 && m.Tie == first[0].Tie))), "Volta não pode ser preparada antes da ida");

        // Setup: the first leg may end level; the second one decides, counting the first leg.
        var l1 = first[0]; var l2 = two.Matches.Single(m => m.Tie == l1.Tie && m.Leg == 2);
        Check(!ClubCompetitions.Setup(g, two, l1).Knockout && ClubCompetitions.Setup(g, two, l1).AggregateLead == 0, "Ida pode terminar empatada, sem pênaltis");
        Score(l1.Fixture, 3, 1);
        var back = ClubCompetitions.Setup(g, two, l2);
        Check(back.Knockout && back.AggregateLead == 1 - 3, "Volta decide e conta o placar da ida (mandante da volta perde por 2 no agregado)");
        ClubCompetitions.Complete(g, l1);
        Check(l1.Winner == "" && two.Stage == 0, "Depois da ida o confronto continua em aberto");

        // Aggregate rules, with crafted results.
        Score(l2.Fixture, 1, 0);          // home of the second leg wins 1-0: aggregate 3-2 for the first leg's home club
        ClubCompetitions.Complete(g, l2);
        Check(l1.Winner == l1.Fixture.Home && l2.Winner == l1.Fixture.Home, "Vence quem tem o melhor agregado, mesmo perdendo a volta");

        var tied = first[1]; var tiedBack = two.Matches.Single(m => m.Tie == tied.Tie && m.Leg == 2);
        Score(tied.Fixture, 2, 1); Score(tiedBack.Fixture, 1, 0);   // aggregate 2-2
        ClubCompetitions.Complete(g, tied);
        Check(Throws(() => ClubCompetitions.Complete(g, tiedBack)), "Agregado empatado sem pênaltis não decide o confronto");
        Score(tiedBack.Fixture, 1, 0, 3, 4);                          // penalties: away club of the second leg wins
        ClubCompetitions.Complete(g, tiedBack);
        Check(tied.Winner == tied.Fixture.Home && tiedBack.Winner == tied.Fixture.Home, "Agregado empatado vai aos pênaltis, sem gol fora de casa");

        var awayGoals = first[2]; var awayGoalsBack = two.Matches.Single(m => m.Tie == awayGoals.Tie && m.Leg == 2);
        Score(awayGoals.Fixture, 1, 1); Score(awayGoalsBack.Fixture, 0, 0, 5, 3);
        ClubCompetitions.Complete(g, awayGoals); ClubCompetitions.Complete(g, awayGoalsBack);
        Check(awayGoals.Winner == awayGoalsBack.Fixture.Home, "Empates de 1 a 1 e 0 a 0 vão aos pênaltis em vez de decidir por gols fora");
        Check(two.Stage == 0, "A fase só avança quando todos os confrontos estão decididos");
        var last = first[3]; var lastBack = two.Matches.Single(m => m.Tie == last.Tie && m.Leg == 2);
        Score(last.Fixture, 0, 2); Score(lastBack.Fixture, 0, 0);
        ClubCompetitions.Complete(g, last); ClubCompetitions.Complete(g, lastBack);
        Check(two.Stage == 1 && two.Matches.Count(m => m.Stage == 1) == 4 && two.Matches.Where(m => m.Stage == 1 && m.Leg == 1).Select(m => m.Fixture.Home).Concat(two.Matches.Where(m => m.Stage == 1 && m.Leg == 1).Select(m => m.Fixture.Away)).OrderBy(x => x).SequenceEqual(first.Select(m => m.Winner).OrderBy(x => x)), "Os quatro vencedores formam a fase seguinte, também em ida e volta");
        Check(two.Matches.Where(m => m.Stage == 1).All(m => m.Fixture.Round is 8 or 9), "Fase seguinte usa a semana da fase e a seguinte para a volta");

        // Final: the champion is known after the second leg, not the first.
        var finalGame = Career();
        var final = ClubCompetitions.Add(finalGame, "final", "Final de teste", Others(finalGame, 2), [5], 1_000_000, 2);
        var f1 = final.Matches.Single(m => m.Leg == 1); var f2 = final.Matches.Single(m => m.Leg == 2);
        Score(f1.Fixture, 0, 1); ClubCompetitions.Complete(finalGame, f1);
        Check(final.Champion == "", "Campeão não existe depois da ida");
        Score(f2.Fixture, 0, 0); ClubCompetitions.Complete(finalGame, f2);
        Check(final.Champion == f1.Fixture.Away && finalGame.News[0].Contains("campeão"), "Campeão é conhecido depois da volta");

        // Single-leg ties are untouched.
        var one = Career();
        var single = ClubCompetitions.Add(one, "unico", "Jogo único", Others(one, 4), [4, 9], 0);
        Check(single.Matches.Count == 2 && single.Matches.All(m => m.Leg == 1 && m.Fixture.Leg == 0) && ClubCompetitions.Setup(one, single, single.Matches[0]) is { Knockout: true, AggregateLead: 0 }, "Jogo único continua decidindo na própria partida");
        Score(single.Matches[0].Fixture, 2, 2);
        Check(Throws(() => ClubCompetitions.Complete(one, single.Matches[0])), "Empate em jogo único continua exigindo pênaltis");

        // Which match of the club is due, week by week.
        var club = Career();
        var mine = ClubCompetitions.Add(club, "meu", "Confronto do clube", [club.ClubId, Others(club, 1)[0]], [6], 0, 2);
        club.Round = 5;
        Check(ClubCompetitions.NextDue(club) == null, "Nenhuma partida é devida antes da semana da ida");
        club.Round = 6;
        var due = ClubCompetitions.NextDue(club)!;
        Check(due.Leg == 1, "Na semana da ida a partida devida é a ida");
        Game.Advance(club);
        Check(club.Round == 7 && mine.Matches.Single(m => m.Leg == 1).Fixture.HomeGoals != null && mine.Matches.Single(m => m.Leg == 2).Fixture.HomeGoals == null && ClubCompetitions.NextDue(club)?.Leg == 2, "Depois da ida, a volta é a partida devida da semana seguinte");
        Game.Advance(club);
        Check(club.Round == 8 && mine.Matches.All(m => m.Fixture.HomeGoals != null), "Jogos do clube são disputados nas semanas da ida e da volta");
        Check(ClubCompetitions.NextDue(club) == null && (mine.Champion != "" || mine.Matches.All(m => m.Winner != "")), "Confronto decidido e nada mais devido");

        // Engine: the shootout depends on the aggregate, not only on the match.
        var setupGame = Game.New("pal");
        MatchSetup Pair(int lead) => new(MatchSimulation.Version, Tactics.Team("fla", setupGame.OpponentSquads["fla"], "4-3-3", "Equilibrada"), Tactics.Team("pal", setupGame.Squad, "4-4-2", "Equilibrada"), Knockout: true, AggregateLead: lead);
        int checkedShootouts = 0, checkedNoShootouts = 0;
        foreach (var lead in new[] { -2, -1, 0, 1, 2 })
            for (int seed = 1; seed <= 16; seed++)
            {
                var replay = MatchSimulation.Run(Pair(lead), seed, false);
                bool level = replay.HomeGoals + lead == replay.AwayGoals;
                bool shootout = replay.HomePenalties != null && replay.AwayPenalties != replay.HomePenalties;
                if (level) { checkedShootouts++; if (!shootout) throw new Exception("Agregado empatado sem pênaltis (vantagem " + lead + ", semente " + seed + ")"); }
                else { checkedNoShootouts++; if (replay.HomePenalties != null && replay.HomePenalties + replay.AwayPenalties > 0) throw new Exception("Pênaltis em confronto já decidido (vantagem " + lead + ", semente " + seed + ")"); }
            }
        Check(checkedShootouts > 0 && checkedNoShootouts > 0, "Motor decide pênaltis só com o agregado empatado (" + checkedShootouts + " disputas e " + checkedNoShootouts + " confrontos já decididos, 80 partidas)");
    }
}

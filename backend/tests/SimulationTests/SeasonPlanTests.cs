using Brasileirao;

// Foundation of the calendar by competition: a season whose length and league start are not fixed at
// 38 rounds from round 1, and weeks in which the club has no match.
public static class SeasonPlanTests
{
    static void Check(bool value, string description)
    {
        if (!value) throw new Exception(description);
        Console.WriteLine("PASS: " + description);
    }
    static bool Throws(Action action) { try { action(); return false; } catch (GameRuleException) { return true; } }

    public static void Run()
    {
        // Default career: 38 rounds from round 1, as before.
        var normal = Game.New("pal");
        Check(normal.SeasonRounds == 38 && normal.LeagueStart == 1, "Carreira nova mantém 38 rodadas a partir da primeira");
        Check(normal.Fixtures.Min(f => f.Round) == 1 && normal.Fixtures.Max(f => f.Round) == 38 && Game.HasLeagueMatch(normal), "Liga padrão ocupa as rodadas 1 a 38 e o clube joga na primeira");
        Check(Throws(() => Game.AdvanceWeek(normal)), "Semana com partida do clube não pode ser avançada sem jogo");
        Check(Throws(() => TransferMarket.NextSeason(normal)), "Próxima temporada continua exigindo a liga concluída");

        // The date of a round no longer depends on a league fixture existing in it.
        Check(normal.Fixtures.All(f => f.Date!.Value.DayNumber - SeasonCalendar.DateOfRound(normal.Season, f.Round).DayNumber is 0 or 1), "Data de cada partida é a da rodada, no sábado ou no domingo");
        Check(SeasonCalendar.DateOfRound(2026, 9).DayNumber - SeasonCalendar.DateOfRound(2026, 8).DayNumber == 14, "Pausa de seleções soma sete dias após a rodada 8");

        // A season whose league starts in round 5 and ends in round 42.
        var late = Game.New("pal");
        late.LeagueStart = 5; late.SeasonRounds = 42;
        late.Fixtures = SeasonCalendar.Generate(late.Clubs, late.Season, late.LotterySeed, "A", late.LeagueStart);
        late.Life!.Competitions.Clear();
        Check(late.Fixtures.Count == 380 && late.Fixtures.Min(f => f.Round) == 5 && late.Fixtures.Max(f => f.Round) == 42, "Liga que começa na rodada 5 ocupa as rodadas 5 a 42");
        Check(late.Fixtures.Where(f => f.Round == 5).All(f => f.Date == SeasonCalendar.DateOfRound(late.Season, 5) || f.Date == SeasonCalendar.DateOfRound(late.Season, 5).AddDays(1)), "Datas acompanham a semana em que a liga começa");
        Check(late.Fixtures.Where(f => f.Round == 42).All(f => f.Kickoff!.Value.Hour == 16), "Última rodada da liga é simultânea, qualquer que seja o seu número");
        Check(!Game.HasLeagueMatch(late), "Clube não tem partida de liga antes do início dela");
        Check(Throws(() => LiveBroadcast.Start(late, DateTimeOffset.UtcNow)), "Transmissão não abre em semana sem partida");
        for (int i = 0; i < 4; i++) Game.AdvanceWeek(late);
        Check(late.Round == 5 && late.Fixtures.All(f => f.HomeGoals == null), "Quatro semanas sem jogo passam sem simular a liga");
        Check(Game.HasLeagueMatch(late) && Throws(() => Game.AdvanceWeek(late)), "Na semana de abertura da liga o clube volta a jogar");
        for (int i = 0; i < 38; i++) Game.Advance(late);
        Check(late.Round == 43 && late.Fixtures.All(f => f.HomeGoals != null), "Liga de 38 rodadas termina na rodada 42");
        Check(Game.Table(late).Length == 20 && Throws(() => Game.Advance(late)), "Depois da rodada 42 a temporada está encerrada");
        var before = late.Season;
        TransferMarket.NextSeason(late);
        Check(late.Season == before + 1 && late.Round == 1 && late.SeasonRounds == 42 && late.LeagueStart == 5, "Nova temporada preserva o tamanho e o início da liga");
        Check(late.Fixtures.Min(f => f.Round) == 5 && late.Fixtures.Max(f => f.Round) == 42 && !Game.HasLeagueMatch(late), "Calendário da nova temporada também começa na rodada 5");

        // Error text follows the season length.
        var shortSeason = Game.New("pal");
        shortSeason.SeasonRounds = 40;
        try { TransferMarket.NextSeason(shortSeason); Check(false, "Próxima temporada antes do fim deveria falhar"); }
        catch (GameRuleException e) { Check(e.Message.Contains("40"), "Mensagem de temporada incompleta cita o tamanho real da temporada"); }
    }
}

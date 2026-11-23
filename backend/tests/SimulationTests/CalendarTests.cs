using Brasileirao;
using System.Text.Json;

public static class CalendarTests
{
    static void Check(bool value, string text) { if (!value) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    public static void Run()
    {
        foreach (int year in new[] { 2026, 2027, 2028, 2035, 2050, 2100 })
        {
            var fixtures = SeasonCalendar.Generate(Game.Clubs, year, "calendar-test");
            Check(fixtures.Count == 380 && fixtures.GroupBy(f => f.Round).All(r => r.Count() == 10 && r.SelectMany(f => new[] { f.Home, f.Away }).Distinct().Count() == 20), $"Calendário {year}: 38 rodadas sem dupla participação");
            Check(fixtures.Select(f => (f.Home, f.Away)).Distinct().Count() == 380 && Game.Clubs.All(c => fixtures.Count(f => f.Home == c.Id) == 19 && fixtures.Count(f => f.Away == c.Id) == 19), $"Calendário {year}: todos enfrentam todos com 19 mandos por clube");
            Check(fixtures.All(f => f.Date?.Year == year && f.Kickoff?.Year == year && f.Kickoff.Value.Date == f.Date.Value.ToDateTime(TimeOnly.MinValue)), $"Datas e horários pertencem ao ano {year}");
            Check(Game.Clubs.All(c => fixtures.Where(f => f.Home == c.Id || f.Away == c.Id).OrderBy(f => f.Kickoff).Zip(fixtures.Where(f => f.Home == c.Id || f.Away == c.Id).OrderBy(f => f.Kickoff).Skip(1)).All(p => p.Second.Kickoff - p.First.Kickoff >= TimeSpan.FromHours(120))), $"Calendário {year}: descanso mínimo de cinco dias");
            Check(fixtures.Where(f => f.Round == 38).Select(f => f.Kickoff).Distinct().Count() == 1, $"Rodada final {year} tem horário simultâneo");
        }
        var a = SeasonCalendar.Generate(Game.Clubs, 2030, "same-save");
        var b = SeasonCalendar.Generate(Game.Clubs.AsEnumerable().Reverse().ToList(), 2030, "same-save");
        Check(JsonSerializer.Serialize(a, Game.Json) == JsonSerializer.Serialize(b, Game.Json), "Mesmo save/ano reproduz o calendário independentemente da ordem do catálogo");
        var next = SeasonCalendar.Generate(Game.Clubs, 2031, "same-save");
        Check(a.Where(f => f.Round == 1).Select(f => (f.Home, f.Away)).Except(next.Where(f => f.Round == 1).Select(f => (f.Home, f.Away))).Any(), "A temporada seguinte recebe nova ordem de confrontos");
        var g = Game.New("pal"); g.CalendarVersion = 0;
        foreach (var f in g.Fixtures) { f.Date = null; f.Kickoff = null; }
        var played = g.Fixtures[0]; played.HomeGoals = 2; played.AwayGoals = 1;
        string before = JsonSerializer.Serialize(played, Game.Json);
        var identities = g.Fixtures.Select(f => (f.Round, f.Home, f.Away, f.Seed)).ToArray();
        SeasonCalendar.Upgrade(g);
        Check(JsonSerializer.Serialize(played, Game.Json) == before && g.Fixtures.Skip(1).All(f => f.Date != null), "Upgrade agenda apenas jogos futuros e preserva a partida disputada");
        Check(identities.SequenceEqual(g.Fixtures.Select(f => (f.Round, f.Home, f.Away, f.Seed))), "Upgrade não altera confrontos, mandos, rodadas ou seeds existentes");
        before = JsonSerializer.Serialize(g, Game.Json); SeasonCalendar.Upgrade(g);
        Check(JsonSerializer.Serialize(g, Game.Json) == before, "Ler o calendário novamente não remarca jogos");
        g = Game.New("pal"); Divisions.EnableDemo(g);
        Check(g.Divisions!.OtherFixtures.All(f => f.Date?.Year == g.Season), "Divisão complementar recebe datas do mesmo ano");
        foreach (var f in g.Fixtures.Concat(g.Divisions.OtherFixtures)) { f.HomeGoals = 0; f.AwayGoals = 0; }
        g.Round = 39; g.Life!.Competitions.Clear(); TransferMarket.NextSeason(g);
        Check(g.Season == 2027 && g.Fixtures.Concat(g.Divisions!.OtherFixtures).All(f => f.Date?.Year == 2027), "Virada anual gera datas futuras para ambas as divisões");
        Check(g.PastSeasons[0].Fixtures.All(f => f.Date?.Year == 2026) && g.PastSeasons[0].Divisions!.Playoffs.All(f => f.Date?.Year == 2026), "Arquivo conserva datas da temporada e dos playoffs anteriores");
    }
}

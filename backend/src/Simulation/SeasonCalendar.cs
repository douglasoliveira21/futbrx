using System.Security.Cryptography;
using System.Text;

namespace Brasileirao;

public static class SeasonCalendar
{
    public const string Version = "generated-v1";
    public const string Source = "Calendário gerado pelo jogo; datas da temporada simulada";

    static Random RandomFor(int season, string careerSeed, string division) => new(BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes($"{Version}:{careerSeed}:{season}:{division}")), 0));
    static void Shuffle<T>(List<T> values, Random rng)
    {
        for (int i = values.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (values[i], values[j]) = (values[j], values[i]); }
    }
    public static List<Fixture> Generate(List<Club> clubs, int season, string careerSeed, string division = "A", int startRound = 1)
    {
        // Twenty clubs (Séries A to C) or the six of a Série D group, in double round robin.
        int n = clubs.Count, half = n / 2;
        if (n is not (20 or 6) || clubs.Select(c => c.Id).Distinct().Count() != n) throw new GameRuleException("O calendário exige vinte clubes distintos (ou seis, na Série D).");
        if (season is < 1 or > 9999) throw new GameRuleException("Ano fora do intervalo suportado pelo calendário.");
        var rng = RandomFor(season, careerSeed, division);
        var ids = clubs.Select(c => c.Id).Order(StringComparer.Ordinal).ToList(); Shuffle(ids, rng);
        List<Fixture> fixtures = [];
        for (int round = 1; round <= n - 1; round++)
        {
            for (int i = 0; i < half; i++)
            {
                var home = ids[i]; var away = ids[n - 1 - i]; if (round % 2 == 0) (home, away) = (away, home);
                fixtures.Add(new() { Round = round + startRound - 1, Home = home, Away = away, Seed = rng.Next() });
                fixtures.Add(new() { Round = round + (n - 1) + startRound - 1, Home = away, Away = home, Seed = rng.Next() });
            }
            var last = ids[^1]; ids.RemoveAt(n - 1); ids.Insert(1, last);
        }
        fixtures = fixtures.OrderBy(f => f.Round).ThenBy(f => f.Home, StringComparer.Ordinal).ToList();
        AssignDates(fixtures, season, careerSeed, division, false);
        return fixtures;
    }
    // Saturday of a weekly round: the first Saturday of February plus the weeks, with a pause of seven days after rounds 8, 16, 24 and 32.
    public static DateOnly DateOfRound(int season, int round)
    {
        var start = new DateOnly(season, 2, 1);
        while (start.DayOfWeek != DayOfWeek.Saturday) start = start.AddDays(1);
        return start.AddDays((round - 1) * 7 + new[] { 8, 16, 24, 32 }.Count(r => round > r) * 7);
    }
    public static void AssignDates(List<Fixture> fixtures, int season, string careerSeed, string division, bool onlyMissingFuture)
    {
        if (fixtures.Count == 0) return;
        var rng = RandomFor(season, careerSeed, division + ":dates");
        int lastRound = fixtures.Max(f => f.Round);
        foreach (var round in fixtures.GroupBy(f => f.Round).OrderBy(r => r.Key))
        {
            var games = round.OrderBy(f => f.Home, StringComparer.Ordinal).ToList(); Shuffle(games, rng);
            for (int i = 0; i < games.Count; i++)
            {
                var f = games[i];
                if (onlyMissingFuture && (f.Date != null || f.HomeGoals != null || f.Setup != null)) continue;
                bool simultaneous = f.Round == lastRound;
                var date = DateOfRound(season, f.Round).AddDays(simultaneous || i >= 4 ? 1 : 0);
                int[] saturday = [15, 17, 19, 21], sunday = [11, 15, 17, 17, 19, 21];
                int hour = simultaneous ? 16 : i < 4 ? saturday[i] : sunday[(i - 4) % sunday.Length];
                f.Date = date;
                f.Kickoff = new DateTimeOffset(date.Year, date.Month, date.Day, hour, 0, 0, TimeSpan.FromHours(-3));
            }
        }
    }
    public static void Upgrade(GameState g)
    {
        if(g.CalendarVersion>=2)return;
        if(g.CalendarVersion==1){var from=g.Round+(g.LiveMatch!=null?1:0);AssignDates(g.Fixtures.Where(f=>f.Round>=from&&f.HomeGoals==null&&f.Setup==null&&!f.Rescheduled).ToList(),g.Season,g.LotterySeed,g.Divisions?.ManagedDivision??"A",false);g.CalendarVersion=2;g.CalendarSource=Source+"; pausas de seleções geradas";return;}
        bool missing = g.Fixtures.Any(f => f.Date == null && f.HomeGoals == null && f.Setup == null);
        AssignDates(g.Fixtures, g.Season, g.LotterySeed, g.Divisions?.ManagedDivision ?? "A", true);
        if (g.Divisions != null) AssignDates(g.Divisions.OtherFixtures, g.Season, g.LotterySeed, g.Divisions.ManagedDivision == "A" ? "B" : "A", true);
        if (missing) g.CalendarSource = Source + "; partidas anteriores preservadas";
        g.CalendarVersion = 2;
    }
}

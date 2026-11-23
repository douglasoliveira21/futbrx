using System.Text.Json;
namespace Brasileirao;

public static class Calendar2026
{
    public sealed record Entry(int Round, string Home, string Away, DateOnly Date, DateTimeOffset? Kickoff);
    public sealed record Calendar(int Season, string Source, string License, string Notice, List<Entry> Fixtures);
    public static readonly Calendar Data = Load();
    static Calendar Load()
    {
        using var stream = typeof(Calendar2026).Assembly.GetManifestResourceStream("Brasileirao.Calendar2026.json")!;
        return JsonSerializer.Deserialize<Calendar>(stream, Game.Json)!;
    }
    public static bool Compatible(GameState g) => g.Season == 2026 && g.Clubs.Select(c => c.Id).ToHashSet().SetEquals(Data.Fixtures.SelectMany(f => new[] { f.Home, f.Away }));
    public static void Apply(GameState g)
    {
        if (g.Round != 1 || g.Fixtures.Any(f => f.HomeGoals != null)) throw new GameRuleException("O calendário só pode ser aplicado antes da primeira rodada.");
        if (!Compatible(g)) throw new GameRuleException("O calendário de 2026 exige exatamente os vinte participantes reais. Esta carreira usa outra composição de clubes.");
        g.Fixtures = Data.Fixtures.Select(f => new Fixture { Round = f.Round, Home = f.Home, Away = f.Away, Date = f.Date, Kickoff = f.Kickoff, Seed = Random.Shared.Next() }).ToList();
        g.CalendarSource = Data.Source;
    }
}

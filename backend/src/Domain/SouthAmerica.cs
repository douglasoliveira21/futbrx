namespace Brasileirao;

// Result of one tournament of a national league (Apertura, Clausura...), with its final table.
public sealed record TournamentResult(string Name, string Champion, List<SeasonStanding> Table);

// A national championship of one season: its tournaments, the aggregate table and the clubs that go to the continental cups.
public sealed class CountryResult
{
    public string Code { get; set; } = "";
    public int Season { get; set; }
    public List<TournamentResult> Tournaments { get; set; } = [];
    public string Champion { get; set; } = "";
    public List<SeasonStanding> Table { get; set; } = [];
    public List<string> Libertadores { get; set; } = [];
    public List<string> Sudamericana { get; set; } = [];
}

// The nine other CONMEBOL leagues, simulated in the background (club strength only); Brazil is the career league.
public sealed class SouthAmericaState
{
    // Season of the continental cups these results qualify for (the results are those of the season before).
    public int Season { get; set; }
    public List<CountryResult> Results { get; set; } = [];
}

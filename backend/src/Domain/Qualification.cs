namespace Brasileirao;

// A place in an international competition and where it came from.
public sealed record Berth(string ClubId, string Competition, string Origin);
public sealed record SeasonBerths(int Season, List<Berth> Berths);

// Regulation of the international places of the Brazilian clubs; one entry per rule change (FromSeason is the first season
// of the competition it applies to).
public sealed record QualificationRules(int FromSeason, int LeagueDirect, int LeaguePre, int SulAmericana, bool ContinentalChampionsDirect);

public sealed class QualificationState
{
    // Places for the season in progress, resolved when the previous one ended.
    public int Season { get; set; }
    public List<Berth> Berths { get; set; } = [];
    public List<SeasonBerths> History { get; set; } = [];
    // Mathematical clinches already announced to the manager.
    public List<string> Clinched { get; set; } = [];
}

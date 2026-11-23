namespace Brasileirao;

public sealed class DivisionSeason
{
    public string ManagedDivision { get; set; } = "A";
    public List<Club> OtherClubs { get; set; } = [];
    public List<Fixture> OtherFixtures { get; set; } = [];
    public List<Fixture> Playoffs { get; set; } = [];
    public List<string> Promoted { get; set; } = [];
    public List<string> Relegated { get; set; } = [];
}

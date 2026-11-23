namespace Brasileirao;

public sealed class SeasonCatalog
{
    public string Version { get; set; } = "";
    public int Season { get; set; }
    public string Source { get; set; } = "";
    public string License { get; set; } = "";
    public List<Club> Clubs { get; set; } = [];
    public List<CatalogPlayer> Players { get; set; } = [];
}

public sealed record CatalogPlayer(string ClubId, Player Player);

public sealed class DataRelease
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Version { get; set; } = "";
    public int Season { get; set; }
    public string Source { get; set; } = "";
    public string License { get; set; } = "";
    public bool Published { get; set; }
    public int Revision { get; set; } = 1;
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
}

public sealed class ClubData
{
    public Guid ReleaseId { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Short { get; set; } = "";
    public string Color { get; set; } = "";
    public int Strength { get; set; }
}

public sealed class PlayerData
{
    public Guid ReleaseId { get; set; }
    public int Id { get; set; }
    public string ClubId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Position { get; set; } = "";
    public int Age { get; set; }
    public int Rating { get; set; }
    public int Value { get; set; }
    public int Wage { get; set; }
}

public sealed class GameRuleException(string message) : Exception(message);

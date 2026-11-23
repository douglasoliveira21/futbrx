namespace Brasileirao;

/// <summary>Estimates a game rating from a player's market value in euros.</summary>
public static class MarketRating
{
    public static int? FromEuros(decimal? value)
    {
        if (value is null or <= 0) return null;
        // The logarithmic scale prevents a few very expensive players from
        // compressing the rest of the squad into the same rating.
        double rating = 45 + 10 * Math.Log10((double)value.Value / 100_000);
        return (int)Math.Round(Math.Clamp(rating, 45, 90), MidpointRounding.AwayFromZero);
    }
}

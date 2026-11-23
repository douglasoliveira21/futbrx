using System.Security.Cryptography;
using System.Text;

namespace Brasileirao;

public static class LeagueRules
{
    public sealed record Standing(Club Club, int Played, int Wins, int Draws, int Losses, int Goals, int Against, int Difference, int Points, int RedCards, int YellowCards);
    public static List<Standing> Table(List<Club> clubs, List<Fixture> fixtures, string drawSeed)
    {
        var rows = clubs.Select(c =>
        {
            var fs = fixtures.Where(f => f.HomeGoals != null && f.AwayGoals != null && (f.Home == c.Id || f.Away == c.Id)).ToList();
            int goals = fs.Sum(f => f.Home == c.Id ? f.HomeGoals!.Value : f.AwayGoals!.Value), against = fs.Sum(f => f.Home == c.Id ? f.AwayGoals!.Value : f.HomeGoals!.Value);
            int wins = fs.Count(f => f.Home == c.Id ? f.HomeGoals > f.AwayGoals : f.AwayGoals > f.HomeGoals), draws = fs.Count(f => f.HomeGoals == f.AwayGoals);
            var events = fs.SelectMany(f => f.Events).Where(e => e.Team == c.Id).ToList();
            return new Standing(c, fs.Count, wins, draws, fs.Count - wins - draws, goals, against, goals - against, wins * 3 + draws, events.Count(e => e.Type == "red_card"), events.Count(e => e.Type is "yellow_card" or "second_yellow"));
        }).ToList();
        return rows.GroupBy(r => (r.Points, r.Wins, r.Difference, r.Goals)).OrderByDescending(g => g.Key.Points).ThenByDescending(g => g.Key.Wins).ThenByDescending(g => g.Key.Difference).ThenByDescending(g => g.Key.Goals)
            .SelectMany(group =>
            {
                var pair = group.ToList(); var head = pair.Count == 2 ? fixtures.Where(f => f.HomeGoals != null && f.AwayGoals != null && pair.Any(r => r.Club.Id == f.Home) && pair.Any(r => r.Club.Id == f.Away)).ToList() : [];
                // Confronto direto só se aplica a dois clubes e após os dois jogos.
                (int Points, int Difference) H(Standing r) => head.Count == 2 ? (head.Sum(f => f.HomeGoals == f.AwayGoals ? 1 : (f.Home == r.Club.Id ? f.HomeGoals > f.AwayGoals : f.AwayGoals > f.HomeGoals) ? 3 : 0), head.Sum(f => f.Home == r.Club.Id ? f.HomeGoals!.Value - f.AwayGoals!.Value : f.AwayGoals!.Value - f.HomeGoals!.Value)) : (0, 0);
                return pair.OrderByDescending(r => H(r).Points).ThenByDescending(r => H(r).Difference).ThenBy(r => r.RedCards).ThenBy(r => r.YellowCards).ThenBy(r => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(drawSeed + ":" + r.Club.Id))), StringComparer.Ordinal);
            }).ToList();
    }
}

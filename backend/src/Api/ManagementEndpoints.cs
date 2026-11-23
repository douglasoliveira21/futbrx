using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Brasileirao;

public static class ManagementEndpoints
{
    public static object CareerView(Career c, GameState g) => new
    {
        c.Id,
        c.Name,
        c.Revision,
        state = new
        {
            liveMatch = g.LiveMatch == null ? null : LiveBroadcast.Public(g.LiveMatch),
            competitionClubs=g.Life?.ExtraClubs??[],
            nextCup=ClubCompetitions.NextDue(g)?.Fixture,
            board=g.Life?.Board,
            coach=g.Life?.Coaches is null?null:new {employed=CoachCareer.IsEmployed(g),name=CoachCareer.Player(g).Name,reputation=CoachCareer.Player(g).Reputation,date=g.Life.Coaches.Date,unreadDismissal=g.Life.Coaches.Dismissals.Any(d=>!d.Acknowledged)},
            g.TrainingVersion,
            g.WeeklyTraining,
            g.SportsVersion,
            g.BenchIds,
            g.Substitutions,
            g.AutoSubstitute,
            g.AutoReplaceUnavailable,
            g.PlayerStatsFromRound,
            playerStats = g.PlayerStats.Where(x => g.Squad.Any(p => p.Id == x.Key)).ToDictionary(),
            g.ManagementVersion,
            g.Loans,
            g.TransferPayments,
            g.DataVersion,
            g.CalendarSource,
            g.CalendarVersion,
            division = g.Divisions?.ManagedDivision ?? "A",
            otherClubs = g.Divisions?.OtherClubs ?? [],
            g.Season,
            g.Round,
            g.ClubId,
            g.Formation,
            g.TacticalPlan,
            g.Mentality,
            g.Training,
            g.Balance,
            g.Clubs,
            g.Squad,
            g.Market,
            g.Fixtures,
            g.Ledger,
            g.News,
            g.History,
            contracts = g.Contracts.Where(x => x.Value.Active && x.Value.ClubId == g.ClubId).ToDictionary(),
            g.Offers,
            g.Transfers,
            pastSeasons = g.PastSeasons.Select(h => new { h.Season, h.Champion, h.Table })
        },
        table = Game.Table(g),
        management = new { window = TransferMarket.Window(g), wageBill = g.Squad.Sum(p => (long)p.Wage), expiringContracts = g.Contracts.Values.Count(x => x.Active && x.ClubId == g.ClubId && x.EndSeason == g.Season) }
    };
    static Guid Owner(ClaimsPrincipal u) => Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);
    static async Task<Career?> Find(Guid id, GameDb db, ClaimsPrincipal u) { var owner = Owner(u); return await db.Careers.SingleOrDefaultAsync(c => c.Id == id && c.UserId == owner); }
    internal static async Task<IResult> Change(Guid id, int revision, GameDb db, ClaimsPrincipal user, string action, Func<GameState, object> mutate,bool allowUnemployed=false)
    {
        var c = await Find(id, db, user); if (c == null) return Results.NotFound();
        if (c.Revision != revision) return Results.Conflict(new { message = "Carreira atualizada em outra sessão. Recarregue e confira os termos novamente." });
        var g = Game.Read(c); LiveBroadcast.EnsureIdle(g);if(!allowUnemployed)ClubLife.EnsureEmployed(g);
        if(CoachCareer.Player(g).Name=="Treinador")CoachCareer.Player(g).Name=(await db.Accounts.SingleAsync(a=>a.Id==c.UserId)).Name;
        var result = mutate(g); Game.Write(c, g);
        db.Audits.Add(new() { UserId = Owner(user), Action = $"career:{id}:{action}" }); await db.SaveChangesAsync(); return Results.Ok(new { c.Revision, result });
    }
    public static void MapManagementEndpoints(this WebApplication app)
    {
        app.MapGet("/api/calendar/2026", () => Results.Ok(Calendar2026.Data));
        var api = app.MapGroup("/api/careers/{id:guid}").RequireAuthorization().RequireRateLimiting("game");
        api.MapGet("/divisions", async (Guid id, GameDb db, ClaimsPrincipal u) =>
        {
            var c = await Find(id, db, u); if (c == null) return Results.NotFound(); var g = Game.Read(c); var d = g.Divisions;
            return Results.Ok(new { divisions = d, managedTable = Game.Table(g), otherTable = d == null ? null : LeagueRules.Table(d.OtherClubs, d.OtherFixtures, g.LotterySeed + ":" + g.Season) });
        });
        api.MapPost("/divisions/enable", (Guid id, RevisionRequest r, GameDb db, ClaimsPrincipal u) => Change(id, r.Revision, db, u, "divisions:enable", g => { Divisions.EnableDemo(g); return new { message = "Divisões demonstrativas ativadas." }; }));
        api.MapPost("/divisions/complete", (Guid id, RevisionRequest r, GameDb db, ClaimsPrincipal u) => Change(id, r.Revision, db, u, "divisions:playoffs", g => { Divisions.Complete(g); return new { message = "Playoffs e acessos definidos." }; }));
        api.MapGet("/advanced-market", async (Guid id, GameDb db, ClaimsPrincipal u) =>
        {
            var c = await Find(id, db, u); if (c == null) return Results.NotFound(); var g = Game.Read(c);
            return Results.Ok(new { g.Loans, g.TransferPayments, window = TransferMarket.Window(g), candidates = TransferMarket.Candidates(g).Where(x => x.ClubId != "").Select(x => new { x.Player.Id, x.Player.Name, x.ClubId, x.Player.Wage, loanFee = AdvancedMarket.LoanFee(x.Player), releaseClause = g.Contracts.GetValueOrDefault(x.Player.Id)?.ReleaseClause ?? 0, releaseWage = x.WageDemand, releaseBonus = TransferMarket.Bonus(x.WageDemand, "inbound"),releaseCommission=ClubLife.AgentFee(g,x.Player.Id,g.Contracts.GetValueOrDefault(x.Player.Id)?.ReleaseClause??0,x.WageDemand), available = x.Available && !g.Loans.Any(l => !l.Returned && l.PlayerId == x.Player.Id) }) });
        });
        api.MapPost("/loans/lend", (Guid id, LendRequest r, GameDb db, ClaimsPrincipal u) => Change(id, r.Revision, db, u, "loan:lend", g => AdvancedMarket.Lend(g, r.PlayerId, r.BorrowerClub, r.Fee)));
        api.MapPost("/loans", (Guid id, LoanRequest r, GameDb db, ClaimsPrincipal u) => Change(id, r.Revision, db, u, "loan:borrow", g => AdvancedMarket.Borrow(g, r.PlayerId, r.Fee)));
        api.MapPost("/market/release", (Guid id, ReleaseRequest r, GameDb db, ClaimsPrincipal u) => Change(id, r.Revision, db, u, "contract:release", g => AdvancedMarket.Release(g, r.PlayerId, r.Clause)));
        api.MapGet("/market", async (Guid id, GameDb db, ClaimsPrincipal u, string? search, string? position, string? club, long? maxFee, string? sort, int? page) =>
        {
            var c = await Find(id, db, u); if (c == null) return Results.NotFound(); var g = Game.Read(c);
            if (search?.Length > 80 || maxFee < 0 || page < 1 || page > 1000 || position != null && !new[] { "GOL", "ZAG", "LAT", "MEI", "ATA" }.Contains(position)) return Results.BadRequest(new { message = "Filtros de mercado inválidos." });
            var candidates = TransferMarket.Candidates(g).Where(p => (string.IsNullOrWhiteSpace(search) || p.Player.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) && (position == null || p.Player.Position == position) && (club == null || p.ClubId == (club == "__free" ? "" : club)) && (maxFee == null || p.AskingFee <= maxFee));
            candidates = sort switch { "fee" => candidates.OrderBy(p => p.AskingFee).ThenBy(p => p.Player.Id), "name" => candidates.OrderBy(p => p.Player.Name, StringComparer.Ordinal).ThenBy(p => p.Player.Id), _ => candidates.OrderByDescending(p => p.Player.Rating).ThenBy(p => p.Player.Id) };
            var all = candidates.ToList(); var current = page ?? 1;
            return Results.Ok(new { total = all.Count, page = current, pageSize = 20, players = all.Skip((current - 1) * 20).Take(20), window = TransferMarket.Window(g) });
        });
        api.MapGet("/market/quote/{playerId:int}", async (Guid id, int playerId, string kind, int years, GameDb db, ClaimsPrincipal u) =>
        { var c = await Find(id, db, u); return c == null ? Results.NotFound() : Results.Ok(TransferMarket.Quote(Game.Read(c), playerId, kind, years)); });
        api.MapPost("/market/offers", (Guid id, OfferRequest r, GameDb db, ClaimsPrincipal u) => Change(id, r.Revision, db, u, "transfer:propose", g => TransferMarket.Propose(g, r.PlayerId, "inbound", r.Fee, r.Wage, r.Years, r.Installments, r.ReleaseClause)));
        api.MapPost("/contracts/{playerId:int}/renew", (Guid id, int playerId, RenewRequest r, GameDb db, ClaimsPrincipal u) => Change(id, r.Revision, db, u, "contract:renew-proposal", g => TransferMarket.Propose(g, playerId, "renewal", 0, r.Wage, r.Years, 1, r.ReleaseClause)));
        api.MapPost("/market/list/{playerId:int}", (Guid id, int playerId, ListRequest r, GameDb db, ClaimsPrincipal u) => Change(id, r.Revision, db, u, "transfer:listing", g => { TransferMarket.List(g, playerId, r.Listed); return new { message = r.Listed ? "Atleta colocado na lista. Propostas podem chegar ao avançar a rodada, durante a janela." : "Atleta retirado da lista." }; }));
        api.MapPost("/market/offers/{offerId:guid}/accept", (Guid id, Guid offerId, RevisionRequest r, GameDb db, ClaimsPrincipal u) => Change(id, r.Revision, db, u, "transfer:accept", g => TransferMarket.Accept(g, offerId)));
        api.MapPost("/market/offers/{offerId:guid}/withdraw", (Guid id, Guid offerId, RevisionRequest r, GameDb db, ClaimsPrincipal u) => Change(id, r.Revision, db, u, "transfer:withdraw", g => { TransferMarket.Withdraw(g, offerId); return new { message = "Negociação encerrada." }; }));
        api.MapGet("/history/{season:int}", async (Guid id, int season, GameDb db, ClaimsPrincipal u) =>
        { var c = await Find(id, db, u); if (c == null) return Results.NotFound(); var historical = Game.Read(c).PastSeasons.Find(s => s.Season == season); return historical == null ? Results.NotFound() : Results.Ok(historical); });
    }
}
public sealed record OfferRequest(int Revision, int PlayerId, long Fee, int Wage, int Years, int Installments = 1, long ReleaseClause = 0);
public sealed record RenewRequest(int Revision, int Wage, int Years, long ReleaseClause = 0);
public sealed record ListRequest(int Revision, bool Listed);
public sealed record RevisionRequest(int Revision);

public sealed record LoanRequest(int Revision, int PlayerId, long Fee);
public sealed record ReleaseRequest(int Revision, int PlayerId, long Clause);

public sealed record LendRequest(int Revision, int PlayerId, string BorrowerClub, long Fee);

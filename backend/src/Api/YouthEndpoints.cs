using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
namespace Brasileirao;

public static class YouthEndpoints
{
    public static void MapYouthEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/careers/{id:guid}/youth").RequireAuthorization().RequireRateLimiting("game");
        api.MapGet("/", async (Guid id, GameDb db, ClaimsPrincipal user) =>
        {
            var owner = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!); var c = await db.Careers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.UserId == owner); if (c == null) return Results.NotFound(); var g = Game.Read(c);
            return Results.Ok(new { c.Revision, g.Season, g.Round, g.Academy, scouting = g.Scouting == null ? null : new { g.Scouting.Season, g.Scouting.StartedRound, g.Scouting.CompletionRound, g.Scouting.ExpiresRound, g.Scouting.Region, g.Scouting.Position, g.Scouting.Status }, prospects = g.YouthProspects, history = g.YouthHistory, regions = Youth.Regions, positions = Youth.Positions, capacity = Youth.Capacity, searchFee = Youth.SearchFee, promotionFee = Youth.PromotionFee, weeklyCost = Youth.WeeklyCost, contractEndSeason = g.Season + 2, professionalCount = g.Squad.Count, balance = g.Balance });
        });
        api.MapPost("/scouting", (Guid id, ScoutingRequest r, GameDb db, ClaimsPrincipal user) => ManagementEndpoints.Change(id, r.Revision, db, user, "youth:scouting", g => { Youth.StartSearch(g, r.Region, r.Position); return new { message = "Observação contratada. O relatório chegará após duas rodadas." }; }));
        api.MapPost("/prospects/{playerId:int}/recruit", (Guid id, int playerId, RevisionRequest r, GameDb db, ClaimsPrincipal user) => ManagementEndpoints.Change(id, r.Revision, db, user, "youth:recruit", g => { Youth.Recruit(g, playerId); return new { message = "Jovem incorporado à base e disponível no treinamento." }; }));
        api.MapPost("/players/{playerId:int}/promote", (Guid id, int playerId, RevisionRequest r, GameDb db, ClaimsPrincipal user) => ManagementEndpoints.Change(id, r.Revision, db, user, "youth:promote", g => { Youth.Promote(g, playerId); return new { message = "Atleta promovido com contrato profissional. Confira a escalação em Elenco." }; }));
    }
}
public sealed record ScoutingRequest(int Revision, string Region, string Position);

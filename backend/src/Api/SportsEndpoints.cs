using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Brasileirao;

public static class SportsEndpoints
{
    public static void MapSportsEndpoints(this WebApplication app)
    {
        app.MapPost("/api/careers/{id:guid}/tactical-plan",(Guid id,TacticalPlanRequest r,GameDb db,ClaimsPrincipal user)=>ManagementEndpoints.Change(id,r.Revision,db,user,"tactical-plan",g=>{Tactics.ValidatePlan(r.Plan,g.Squad.Select(p=>p.Id));if(r.Formation!=""||r.Mentality!=""){if(!Tactics.Formations.Contains(r.Formation)||!Tactics.Mentalities.Contains(r.Mentality))throw new GameRuleException("Estratégia inválida.");g.Formation=r.Formation;g.Mentality=r.Mentality;}g.TacticalPlan=r.Plan;return new{message="Instruções registradas para a próxima partida."};})).RequireAuthorization().RequireRateLimiting("game");
        var api = app.MapGroup("/api/careers/{id:guid}/sports").RequireAuthorization().RequireRateLimiting("game");
        api.MapGet("/tactical-catalog",(Guid id,GameDb db,ClaimsPrincipal user)=>ReadTacticalCatalog(id,db,user));
        api.MapPost("/tactical-presets",(Guid id,TacticalPresetRequest r,GameDb db,ClaimsPrincipal user)=>ManagementEndpoints.Change(id,r.Revision,db,user,"tactics:save",g=>TacticalPlans.Save(g,r.Id,r.Name,r.FormationId,r.Mentality,r.Plan)));
        api.MapPost("/tactical-presets/delete",(Guid id,TacticalPresetDelete r,GameDb db,ClaimsPrincipal user)=>ManagementEndpoints.Change(id,r.Revision,db,user,"tactics:delete",g=>{TacticalPlans.Delete(g,r.Id);return new{message="Plano removido."};}));
        api.MapPost("/tactical-presets/apply",(Guid id,TacticalPresetDelete r,GameDb db,ClaimsPrincipal user)=>ManagementEndpoints.Change(id,r.Revision,db,user,"tactics:apply",g=>{var preset=TacticalPlans.ForClub(g).Find(p=>p.Id==r.Id)??throw new GameRuleException("Plano indisponível.");g.TacticalPlan=TacticalPlans.Materialize(g,preset);g.Mentality=preset.Mentality;return new{message="Plano aplicado à próxima partida."};}));
        api.MapPost("/plan", (Guid id, SportsPlanRequest r, GameDb db, ClaimsPrincipal user) => ManagementEndpoints.Change(id, r.Revision, db, user, "sports:plan", g =>
        { Sports.SavePlan(g, r.Substitutions, r.AutoSubstitute, r.AutoReplaceUnavailable); return new { message = "Plano salvo para a próxima partida. Trocas automáticas e programadas compartilham o limite de cinco." }; }));
        api.MapPost("/lineup/auto", (Guid id, RevisionRequest r, GameDb db, ClaimsPrincipal user) => ManagementEndpoints.Change(id, r.Revision, db, user, "sports:auto-lineup", g =>
        { Sports.RepairSelection(g.Squad, false); g.Substitutions.Clear(); Sports.RefreshBench(g); return new { message = $"Escalação atualizada: {g.Squad.Count(p => p.Starter)}/11 titulares disponíveis. Planos antigos foram removidos." }; }));
        api.MapGet("/statistics", async (Guid id, string? club, string? sort, GameDb db, ClaimsPrincipal user) =>
        {
            var owner = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!); var c = await db.Careers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.UserId == owner); if (c == null) return Results.NotFound(); var g = Game.Read(c);
            var stats = g.PlayerStats.Values.Where(p => club == null ? g.Clubs.Any(c => c.Id == p.ClubId) : p.ClubId == club);
            stats = sort switch { "minutes" => stats.OrderByDescending(p => p.Minutes).ThenBy(p => p.PlayerId), "cards" => stats.OrderByDescending(p => p.RedCards * 10 + p.YellowCards).ThenBy(p => p.PlayerId), "passes" => stats.OrderByDescending(p => p.Passes).ThenBy(p => p.PlayerId), _ => stats.OrderByDescending(p => p.Goals).ThenByDescending(p => p.Shots).ThenBy(p => p.PlayerId) };
            return Results.Ok(new { season = g.Season, fromRound = g.PlayerStatsFromRound, players = stats.Take(100) });
        });
    }
    static async Task<IResult> ReadTacticalCatalog(Guid id,GameDb db,ClaimsPrincipal user){var owner=Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);var career=await db.Careers.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==id&&c.UserId==owner);if(career==null)return Results.NotFound();var g=Game.Read(career);return Results.Ok(new{formations=FormationCatalog.Templates.Select(t=>new{t.Id,t.Label,t.Category,positions=FormationCatalog.Positions(t)}),plans=TacticalPlans.ForClub(g)});}
}
public sealed record TacticalPresetRequest(int Revision,string Id,string Name,string FormationId,string Mentality,TacticalPlan Plan);
public sealed record TacticalPresetDelete(int Revision,string Id);
public sealed record SportsPlanRequest(int Revision, List<SubstitutionPlan> Substitutions, bool AutoSubstitute, bool AutoReplaceUnavailable);
public sealed record TacticalPlanRequest(int Revision,TacticalPlan Plan,string Formation="",string Mentality="");

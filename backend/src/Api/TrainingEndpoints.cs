using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Brasileirao;

public static class TrainingEndpoints
{
    public static void MapTrainingEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/careers/{id:guid}/training").RequireAuthorization().RequireRateLimiting("game");
        api.MapGet("/", async (Guid id, GameDb db, ClaimsPrincipal user) =>
        {
            var owner = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var c = await db.Careers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.UserId == owner); if (c == null) return Results.NotFound(); var g = Game.Read(c);
            return Results.Ok(new { c.Revision, g.Season, g.Round, plan = g.WeeklyTraining, players = g.Squad.Concat(g.Academy).Select(p => new { player = p, development = g.Development[p.Id], group = g.Academy.Contains(p) ? "Base" : "Profissional" }), reports = g.TrainingReports, sessions = Training.Sessions, intensities = Training.Intensities, presets = new[] { "Técnico", "Físico", "Recuperação" }.Select(name => new { name, plan = Training.Preset(name) }) });
        });
        api.MapPost("/plan", (Guid id, TrainingPlanRequest r, GameDb db, ClaimsPrincipal user) => ManagementEndpoints.Change(id, r.Revision, db, user, "training:plan", g =>
        { Training.SavePlan(g, r.Plan); return new { message = "Planejamento salvo. O treino será aplicado ao avançar a próxima rodada." }; }));
        api.MapPost("/players/{playerId:int}/focus", (Guid id, int playerId, TrainingFocusRequest r, GameDb db, ClaimsPrincipal user) => ManagementEndpoints.Change(id, r.Revision, db, user, "training:focus", g =>
        { Training.SetFocus(g, playerId, r.Focus); return new { message = "Foco individual salvo para os próximos treinos." }; }));
    }
}
public sealed record TrainingPlanRequest(int Revision, TrainingPlan Plan);
public sealed record TrainingFocusRequest(int Revision, string Focus);

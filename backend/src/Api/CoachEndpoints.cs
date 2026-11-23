using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
namespace Brasileirao;

public static class CoachEndpoints
{
    public static void MapCoachEndpoints(this WebApplication app)
    {
        var api=app.MapGroup("/api/careers/{id:guid}/coach").RequireAuthorization().RequireRateLimiting("game");
        api.MapGet("",async(Guid id,GameDb db,ClaimsPrincipal u)=>{
            var owner=Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);var career=await db.Careers.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==id&&c.UserId==owner);if(career==null)return Results.NotFound();var state=Game.Read(career);if(CoachCareer.Player(state).Name=="Treinador")CoachCareer.Player(state).Name=(await db.Accounts.SingleAsync(a=>a.Id==owner)).Name;return Results.Ok(CoachCareer.View(state));
        });
        // SaveChanges uses the snapshot's revision concurrency token. Contracts, money,
        // vacancy allocation and audit are committed together or all rolled back.
        api.MapPost("/actions",(Guid id,CoachAction r,GameDb db,ClaimsPrincipal u)=>ManagementEndpoints.Change(id,r.Revision,db,u,"coach:"+r.Type,g=>CoachCareer.Act(g,r),true));
    }
}

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
namespace Brasileirao;

public static class ClubLifeEndpoints
{
    public static void MapClubLifeEndpoints(this WebApplication app)
    {
        var api=app.MapGroup("/api/careers/{id:guid}/life").RequireAuthorization().RequireRateLimiting("game");
        static async Task<Career?> Find(Guid id,GameDb db,ClaimsPrincipal u)=>await db.Careers.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==id&&c.UserId==Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!));
        api.MapGet("",async(Guid id,GameDb db,ClaimsPrincipal u)=>{var c=await Find(id,db,u);return c==null?Results.NotFound():Results.Ok(ClubLife.View(Game.Read(c)));});
        api.MapPost("/actions",(Guid id,ClubLifeRequest r,GameDb db,ClaimsPrincipal u)=>ManagementEndpoints.Change(id,r.Revision,db,u,"life:"+r.Type,g=>ClubLife.Act(g,r.Type,r.PlayerId,r.Value,r.Amount),r.Type=="job"));
        api.MapGet("/replay/{matchId}",async(Guid id,string matchId,GameDb db,ClaimsPrincipal u)=>{
            var c=await Find(id,db,u);if(c==null)return Results.NotFound();var g=Game.Read(c);
            var match=g.Life!.Competitions.Concat(g.Life.CupHistory.Select(h=>h.Competition)).SelectMany(c=>c.Matches).FirstOrDefault(m=>m.Id==matchId);
            if(match?.Winner is null or ""||match.Fixture.Setup==null)return Results.NotFound();var f=match.Fixture;var replay=MatchSimulation.Run(f.Setup,f.Seed,true);
            if(replay.HomeGoals!=f.HomeGoals||replay.AwayGoals!=f.AwayGoals||replay.HomePenalties!=f.HomePenalties||replay.AwayPenalties!=f.AwayPenalties)throw new InvalidOperationException("Replay eliminatório divergiu do resultado salvo.");return Results.Ok(replay);
        });
    }
}
public sealed record ClubLifeRequest(int Revision,string Type,int PlayerId=0,string Value="",long Amount=0);

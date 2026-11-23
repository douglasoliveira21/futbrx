using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Brasileirao;

public static class LiveMatchEndpoints
{
    static readonly SemaphoreSlim[] Gates=Enumerable.Range(0,64).Select(_=>new SemaphoreSlim(1,1)).ToArray();
    sealed class Lease(SemaphoreSlim gate):IDisposable {public void Dispose()=>gate.Release();}
    static async Task<IDisposable> Enter(Guid id){var gate=Gates[(uint)id.GetHashCode()%64];await gate.WaitAsync();return new Lease(gate);}
    static LiveSimulation Runtime(IMemoryCache cache,Guid id,LiveMatch live)=>cache.GetOrCreate("live-engine:"+id+":"+live.Id,e=>{e.SlidingExpiration=TimeSpan.FromMinutes(20);e.RegisterPostEvictionCallback((_,value,_,_)=>(value as LiveSimulation)?.Dispose());return new LiveSimulation();})!;

    public static void MapLiveMatchEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api/careers/{id:guid}/live").RequireAuthorization().RequireRateLimiting("game");
        static async Task<Career?> Find(Guid id, GameDb db, ClaimsPrincipal user) => await db.Careers.SingleOrDefaultAsync(c => c.Id == id && c.UserId == Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!));
        api.MapPost("/start", async (Guid id, RevisionRequest r, GameDb db, ClaimsPrincipal user, TimeProvider clock) =>
        {
            using var lease=await Enter(id);
            var c = await Find(id, db, user); if (c == null) return Results.NotFound();
            var g = Game.Read(c);
            if (g.LiveMatch == null && c.Revision != r.Revision) return Results.Conflict(new { message = "Recarregue a carreira antes de entrar em campo." });
            var live = LiveBroadcast.Start(g, clock.GetUtcNow()); Game.Write(c, g); await db.SaveChangesAsync();
            return Results.Ok(LiveBroadcast.Public(live));
        });
        api.MapPost("/tick", async (Guid id, BroadcastTick r, GameDb db, ClaimsPrincipal user, TimeProvider clock, IMemoryCache cache) =>
        {
            using var lease=await Enter(id);
            var c = await Find(id, db, user); if (c == null) return Results.NotFound();
            var g = Game.Read(c); var live = g.LiveMatch;
            if (live == null || live.Id != r.Id) return Results.NotFound();
            LiveBroadcast.Tick(live, clock.GetUtcNow(), r.Playing, r.ClientId);
            MatchReplay replay;
            if (live.Incremental)
            {
                var runtime = Runtime(cache,id,live);
                replay = runtime.Advance(live);
            }
            else replay = cache.GetOrCreate("broadcast:" + id + ":" + live.Id, entry => { entry.SlidingExpiration = TimeSpan.FromMinutes(20); return MatchSimulation.Run(live.Fixture.Setup!, live.Fixture.Seed, true); })!;
            Game.Write(c, g); await db.SaveChangesAsync();
            // Never send future events, final scores or future frames through the broadcast route.
            var second = LiveBroadcast.MatchSecond(live);
            var frames = replay.Frames.Where(f => f.Second <= second && f.Second > r.After).ToList();
            var events = replay.Events.Where(e => (e.Second >= 0 ? e.Second : e.Minute * 60) <= second).ToList();
            return Results.Ok(new { session = LiveBroadcast.Public(live), complete = LiveBroadcast.Complete(live), replay = new MatchReplay(replay.EngineVersion, replay.Duration, replay.Setup, events, frames, events.Count(e => e.Type == "goal" && e.Team == live.Fixture.Home), events.Count(e => e.Type == "goal" && e.Team == live.Fixture.Away)) });
        });
        api.MapPost("/tactical-presets", async (Guid id, LiveTacticalPresetRequest request, GameDb db, ClaimsPrincipal user) =>
        {
            using var lease = await Enter(id);
            var career = await Find(id, db, user);
            if (career == null) return Results.NotFound();
            var game = Game.Read(career);
            if (game.LiveMatch?.Id != request.MatchId || game.LiveMatch.ManagedTeam != game.ClubId)
                return Results.NotFound();

            var preset = TacticalPlans.Save(game, "", request.Name, request.FormationId, request.Mentality, request.Plan);
            Game.Write(career, game);
            db.Audits.Add(new() { UserId = career.UserId, Action = $"career:{id}:tactics:save-live" });
            await db.SaveChangesAsync();
            return Results.Ok(preset);
        });
        api.MapPost("/control",async(Guid id,BroadcastControl r,GameDb db,ClaimsPrincipal user,TimeProvider clock,IMemoryCache cache)=>
        {
            using var lease=await Enter(id);
            var c=await Find(id,db,user);if(c==null)return Results.NotFound();
            var g=Game.Read(c);var live=g.LiveMatch;if(live==null||live.Id!=r.Id)return Results.NotFound();
            var replay=Runtime(cache,id,live).Advance(live);
            if(r.Type=="preset"){
                var saved=TacticalPlans.ForClub(g).Find(p=>p.Id==r.PresetId)??throw new GameRuleException("Plano salvo indisponível neste clube.");
                var team=live.ManagedTeam==live.Fixture.Setup!.Home.Id?live.Fixture.Setup.Home:live.Fixture.Setup.Away;
                var plan=TacticalPlans.MaterializeLive(saved,team,replay);
                var last=live.Fixture.Setup.Instructions?.LastOrDefault(c=>c.Team==team.Id&&c.Type=="tactics");
                LiveMatchControls.Apply(live,replay,"tactics",12,last?.Formation??team.Formation,saved.Mentality,0,0,clock.GetUtcNow(),plan);
            }
            else if(r.Type=="substitutions")LiveMatchControls.ApplyBatch(live,replay,r.Substitutions,clock.GetUtcNow());
            else LiveMatchControls.Apply(live,replay,r.Type,r.Speed,r.Formation,r.Mentality,r.OutPlayerId,r.InPlayerId,clock.GetUtcNow(),r.Plan);
            Game.Write(c,g);await db.SaveChangesAsync();return Results.Ok(LiveBroadcast.Public(live));
        });
        api.MapPost("/finish", async (Guid id, BroadcastFinish r, GameDb db, ClaimsPrincipal user) =>
        {
            using var lease=await Enter(id);
            var c = await Find(id, db, user); if (c == null) return Results.NotFound();
            var g = LiveBroadcast.Finish(Game.Read(c), r.Id); Game.Write(c, g);
            db.Audits.Add(new() { UserId = c.UserId, Action = $"career:{id}:broadcast-complete" }); await db.SaveChangesAsync();
            return Results.Ok(ManagementEndpoints.CareerView(c, g));
        });
    }
}
public sealed record BroadcastTick(Guid Id, bool Playing, int After = -1, string ClientId = "");
public sealed record BroadcastFinish(Guid Id);

public sealed record BroadcastControl(Guid Id,string Type,int Speed=12,string Formation="",string Mentality="",int OutPlayerId=0,int InPlayerId=0,TacticalPlan? Plan=null,List<SubstitutionCommand>? Substitutions=null,string PresetId="");

public sealed record LiveTacticalPresetRequest(Guid MatchId, string Name, string FormationId, string Mentality, TacticalPlan Plan);

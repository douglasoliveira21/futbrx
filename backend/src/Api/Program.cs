using Microsoft.Extensions.Caching.Memory;
using Brasileirao;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var localDirectory = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../../.runtime"));
var key = builder.Configuration["Jwt:Key"];
if (key == null && builder.Environment.IsDevelopment())
{
    Directory.CreateDirectory(localDirectory);
    var keyFile = Path.Combine(localDirectory, "local-jwt.key");
    if (!File.Exists(keyFile)) File.WriteAllText(keyFile, Convert.ToHexString(RandomNumberGenerator.GetBytes(48)));
    key = File.ReadAllText(keyFile).Trim();
}
key = key ?? throw new InvalidOperationException("Configure Jwt__Key com pelo menos 32 caracteres aleatórios.");
builder.Configuration["Jwt:Key"] = key;
if (key.Length < 32) throw new InvalidOperationException("Jwt__Key precisa de pelo menos 32 caracteres.");
var memory = builder.Environment.IsDevelopment() && builder.Configuration["Database:InMemory"] == "true";
var sqlite = builder.Environment.IsDevelopment() && !memory && builder.Configuration.GetConnectionString("Game") == null;
if (sqlite) Directory.CreateDirectory(localDirectory);
builder.Services.AddDbContext<GameDb>(o => { if (sqlite) o.UseSqlite(builder.Configuration["Database:LocalConnection"] ?? $"Data Source={Path.Combine(localDirectory, "local-game.db")}"); else if (memory) o.UseInMemoryDatabase("development-only"); else o.UseNpgsql(builder.Configuration.GetConnectionString("Game") ?? throw new InvalidOperationException("Configure ConnectionStrings__Game."), npg => npg.MigrationsAssembly("Api")); });
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 2_000_000);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new() { ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true, ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"], IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ClockSkew = TimeSpan.FromSeconds(15) };
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async ctx =>
    {
        if (!Guid.TryParse(ctx.Principal?.FindFirstValue("session_id"), out var sessionId) ||
            !Guid.TryParse(ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) { ctx.Fail("Sessão inválida."); return; }
        var db = ctx.HttpContext.RequestServices.GetRequiredService<GameDb>();
        if (!await db.Sessions.AnyAsync(s => s.Id == sessionId && s.UserId == userId && s.Expires > DateTime.UtcNow)) ctx.Fail("Sessão revogada.");
    }
    };
});
builder.Services.AddAuthorization(o => o.AddPolicy("Admin", policy => policy.RequireAuthenticatedUser().RequireAssertion(ctx => AdminAccess.Allowed(builder.Configuration, ctx.User))));
builder.Services.AddScoped<AuthSession>();
builder.Services.AddHostedService<SessionCleanup>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>(); builder.Services.AddHttpClient<IGoogleTokenVerifier, GoogleTokenVerifier>(c => c.Timeout = TimeSpan.FromSeconds(8)); builder.Services.AddMemoryCache(); builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSignalR(); builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(o => { o.RejectionStatusCode = 429; o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new() { PermitLimit = builder.Configuration.GetValue("RateLimit:AuthPermits", 12), Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); o.AddPolicy("game", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous", _ => new() { PermitLimit = builder.Configuration.GetValue<int?>("RateLimit:GamePerMinute") ?? 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })); });
var app = builder.Build();
CoachCareer.Defaults=builder.Configuration.GetSection("CoachCareer").Get<CoachBalance>()??new();
if (memory || sqlite) { using var scope = app.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<GameDb>().Database.EnsureCreated(); }
if (!memory && !sqlite && builder.Configuration["Database:Migrate"] == "true") { using var scope = app.Services.CreateScope(); scope.ServiceProvider.GetRequiredService<GameDb>().Database.Migrate(); }
await app.SeedCatalog();
app.Use(async (ctx, next) => { ctx.Response.Headers["X-Content-Type-Options"] = "nosniff"; ctx.Response.Headers["Referrer-Policy"] = "same-origin"; try { await next(); } catch (DbUpdateConcurrencyException) { ctx.Response.StatusCode = 409; await ctx.Response.WriteAsJsonAsync(new { message = "Os dados mudaram em outra sessão. Recarregue e tente novamente." }); } catch (GameRuleException e) { ctx.Response.StatusCode = 400; await ctx.Response.WriteAsJsonAsync(new { message = e.Message }); } });
app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
app.MapHealthChecks("/api/health");
app.MapAuthEndpoints();
app.MapCatalogEndpoints();
app.MapAdminEndpoints();
app.MapManagementEndpoints();
app.MapSportsEndpoints();
app.MapLiveMatchEndpoints();
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/development/simulation-v11", (Microsoft.Extensions.Caching.Memory.IMemoryCache cache, int? seed, string? engine) =>
        cache.GetOrCreate($"{(engine is "v11" or "v12" or "v13" ? engine : "current")}-browser-validation-{seed??117}", entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            var game = Game.New("pal");
            // A fixed seed also requires fixed teams; the generated calendar can
            // change its opening fixture between API processes.
            var fixture = game.Fixtures.Single(f => f.Home == "fla" && f.Away == "pal");
            fixture.Seed = seed??117;
            fixture.Setup = new MatchSetup(engine == "v11" ? PositionalV11.Version : engine == "v12" ? PositionalV12.Version : engine == "v13" ? PositionalV13.Version : MatchSimulation.Version,
                Tactics.Team(fixture.Home, fixture.Home == game.ClubId ? game.Squad : game.OpponentSquads[fixture.Home], "4-3-3", "Equilibrada"),
                Tactics.Team(fixture.Away, fixture.Away == game.ClubId ? game.Squad : game.OpponentSquads[fixture.Away], "4-4-2", "Equilibrada"));
            var replay = MatchSimulation.Run(fixture.Setup, fixture.Seed, true);
            return new { fixture, clubs = game.Clubs, replay };
        }));
}

app.MapTrainingEndpoints();
app.MapYouthEndpoints();
app.MapClubLifeEndpoints();
app.MapCoachEndpoints();
var api = app.MapGroup("/api/careers").RequireAuthorization().RequireRateLimiting("game");
// O proprietário é sempre obtido do JWT validado.
api.MapGet("/", async (GameDb db, ClaimsPrincipal u) => await db.Careers.Where(c => c.UserId == Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!)).Select(c => new { c.Id, c.Name, c.Updated, c.Revision }).ToListAsync());
api.MapPost("/", async (NewCareer r, GameDb db, ClaimsPrincipal u) =>
{
    if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 60) return Results.BadRequest(new { message = "Nomeie a carreira (até 60 caracteres)." });
    if (await db.Careers.CountAsync(c => c.UserId == Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!)) >= 10) return Results.BadRequest(new { message = "Limite de dez carreiras por conta." });
    var release = await CatalogEndpoints.ChooseRelease(db, r.ReleaseId);
    if (release == null) return Results.BadRequest(new { message = "Escolha uma versão de dados publicada." });
    var data = await new CatalogStore(db).Read(release.Id);
    if (!data.Clubs.Any(c => c.Id == r.ClubId)) return Results.BadRequest(new { message = "Clube ausente nesta versão." });
    var c = new Career { UserId = Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!), Name = r.Name.Trim() };
    var state=Game.New(r.ClubId,data);CoachCareer.Player(state).Name=(await db.Accounts.SingleAsync(a=>a.Id==c.UserId)).Name;
    Game.Write(c, state); db.Add(c); await db.SaveChangesAsync(); return Results.Ok(new { c.Id });
});
api.MapGet("/{id:guid}", async (Guid id, GameDb db, ClaimsPrincipal u) => { var c = await db.Careers.SingleOrDefaultAsync(c => c.Id == id && c.UserId == Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!)); return c == null ? Results.NotFound() : Results.Ok(ManagementEndpoints.CareerView(c, Game.Read(c))); });
api.MapGet("/{id:guid}/matches/{round:int}/{home}/replay", async (Guid id, int round, string home, int? season, GameDb db, ClaimsPrincipal u) =>
{
    var career = await db.Careers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id && c.UserId == Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!));
    if (career == null) return Results.NotFound();
    var game = Game.Read(career);
    var past = game.PastSeasons.Find(h => h.Season == season);
    var fixtures = season == null || season == game.Season ? game.Fixtures.Concat(game.Divisions?.OtherFixtures ?? []).Concat(game.Divisions?.Playoffs ?? []).ToList() : past?.Fixtures.Concat(past.Divisions?.OtherFixtures ?? []).Concat(past.Divisions?.Playoffs ?? []).ToList();
    var f = fixtures?.Find(f => f.Round == round && f.Home == home);
    if (f == null || f.HomeGoals == null) return Results.NotFound();
    if (f.Setup == null) return Results.Conflict(new { message = "Partida da versão anterior: não há frames posicionais salvos. Use a reprodução de eventos." });
    var replay = MatchSimulation.Run(f.Setup, f.Seed, true);
    if (replay.HomeGoals != f.HomeGoals || replay.AwayGoals != f.AwayGoals) throw new InvalidOperationException("Replay diverge do resultado persistido.");
    return Results.Ok(replay);
});
api.MapPost("/{id:guid}/actions", async (Guid id, ActionRequest r, GameDb db, ClaimsPrincipal u) =>
{
    var c = await db.Careers.SingleOrDefaultAsync(c => c.Id == id && c.UserId == Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!)); if (c == null) return Results.NotFound(); if (c.Revision != r.Revision) return Results.Conflict(new { message = "Carreira atualizada em outra sessão. Recarregue." }); var g = Game.Read(c);ClubLife.EnsureEmployed(g);
    LiveBroadcast.EnsureIdle(g);
    switch (r.Type)
    {
        case "advance": return Results.BadRequest(new { message = "Entre em campo e acompanhe a transmissão até o apito final." });
        case "tactics": if (!new[] { "4-3-3", "4-4-2", "3-5-2" }.Contains(r.Value) || !new[] { "Equilibrada", "Ofensiva", "Defensiva" }.Contains(r.Extra)) return Results.BadRequest(); g.Formation = r.Value; g.Mentality = r.Extra; break;
        case "training": if (!new[] { "Técnico", "Físico", "Recuperação" }.Contains(r.Value)) return Results.BadRequest(); Training.SetPreset(g, r.Value); break;
        case "swap": if (!int.TryParse(r.Value, out var incomingId)) return Results.BadRequest(); Sports.Swap(g, r.PlayerId, incomingId); break;
        case "starter": var p1 = g.Squad.Find(p => p.Id == r.PlayerId); if (p1 == null) return Results.NotFound(); if (!p1.Starter && !Sports.Eligible(p1)) return Results.BadRequest(new { message = Sports.Reason(p1) }); if (!p1.Starter && g.Squad.Count(p => p.Starter) >= 11) return Results.BadRequest(new { message = "Retire um titular antes de escalar outro jogador." }); p1.Starter = !p1.Starter; Sports.RefreshBench(g); g.Substitutions.Clear(); break;
        case "buy": case "sell": return Results.BadRequest(new { message = "Use o mercado de propostas para negociar e registrar o contrato." });
        case "season": TransferMarket.NextSeason(g); break;
        default: return Results.BadRequest();
    }
    Game.Write(c, g); db.Audits.Add(new() { UserId = Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = $"career:{id}:{r.Type}" }); await db.SaveChangesAsync(); return Results.Ok(new { c.Revision });
});
app.Run();

record NewCareer(string Name, string ClubId, Guid? ReleaseId = null);
record ActionRequest(string Type, int Revision, string Value = "", string Extra = "", int PlayerId = -1);
public partial class Program { }







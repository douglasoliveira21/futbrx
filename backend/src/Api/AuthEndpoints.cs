using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Brasileirao;

public static class AdminAccess
{
    // IDs de contas existentes, definidos pelo operador. Cadastro público nunca concede o papel.
    // A lista aceita IDs de conta ou e-mails. O cadastro não confirma o e-mail: crie a conta do administrador antes de abrir o jogo.
    public static bool Allowed(IConfiguration config, ClaimsPrincipal user)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return false;
        var email = user.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        return (config["Admin:UserIds"] ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(s => Guid.TryParse(s, out var configured) ? configured == id
                : email != null && s.Contains('@') && string.Equals(s, email, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed class AuthSession(GameDb db, IConfiguration config, IWebHostEnvironment environment)
{
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    // remember = false: the session lasts one day and the refresh cookie ends with the browser. A refresh keeps what the session started with.
    public async Task<IResult> Issue(Account account, HttpContext context, Session? session = null, bool remember = true)
    {
        if (session != null) remember = session.Expires - session.Created > TimeSpan.FromDays(2);
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        if (session == null)
        {
            session = new Session { UserId = account.Id, Device = context.Request.Headers.UserAgent.ToString()[..Math.Min(context.Request.Headers.UserAgent.ToString().Length, 180)] };
            db.Sessions.Add(session);
        }
        session.Hash = Hash(raw); session.Expires = DateTime.UtcNow.AddDays(remember ? 14 : 1);
        await db.SaveChangesAsync();
        context.Response.Cookies.Append("bm_refresh", raw, new() { HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Strict, Path = "/api/auth", MaxAge = remember ? TimeSpan.FromDays(14) : null });
        var key = config["Jwt:Key"]!;
        var jwt = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"],
            [new(ClaimTypes.NameIdentifier, account.Id.ToString()), new(ClaimTypes.Name, account.Name), new(ClaimTypes.Email, account.Email), new("session_id", session.Id.ToString())],
            expires: DateTime.UtcNow.AddMinutes(15), signingCredentials: new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(jwt), user = new { account.Id, account.Name, account.Email } });
    }
}

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth");
        auth.MapGoogleSignIn();
        auth.MapPasswordReset();
        auth.MapPost("/register", async (Credentials r, GameDb db, AuthSession sessions, HttpContext ctx) =>
        {
            var email = (r.Email ?? "").Trim().ToLowerInvariant();
            if (!System.Net.Mail.MailAddress.TryCreate(email, out _) || email.Length > 254 || string.IsNullOrEmpty(r.Password) || r.Password.Length is < 12 or > 128 || string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > 60)
                return Results.BadRequest(new { message = "Informe nome, e-mail válido e senha entre 12 e 128 caracteres." });
            if (await db.Accounts.AnyAsync(a => a.Email == email)) return Results.Conflict(new { message = "Não foi possível cadastrar este e-mail." });
            var a = new Account { Email = email, Name = r.Name.Trim() };
            a.PasswordHash = new PasswordHasher<Account>().HashPassword(a, r.Password);
            db.Add(a); db.Audits.Add(new() { UserId = a.Id, Action = "auth:register" });
            try { return await sessions.Issue(a, ctx, null, r.Remember); }
            catch (DbUpdateException) { return Results.Conflict(new { message = "Não foi possível cadastrar este e-mail." }); }
        }).RequireRateLimiting("auth");
        auth.MapPost("/login", async (Credentials r, GameDb db, AuthSession sessions, HttpContext ctx) =>
        {
            if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrEmpty(r.Password) || r.Password.Length > 128) return Results.Unauthorized();
            var a = await db.Accounts.SingleOrDefaultAsync(a => a.Email == r.Email.Trim().ToLowerInvariant());
            if (a != null && GoogleSignIn.IsGoogleAccount(a)) return Results.Json(new { message = "Esta conta entra com o Google." }, statusCode: 401);
            if (a == null || new PasswordHasher<Account>().VerifyHashedPassword(a, a.PasswordHash, r.Password) == PasswordVerificationResult.Failed)
                return Results.Json(new { message = "E-mail ou senha incorretos." }, statusCode: 401);
            db.Audits.Add(new() { UserId = a.Id, Action = "auth:login" });
            return await sessions.Issue(a, ctx, null, r.Remember);
        }).RequireRateLimiting("auth");
        auth.MapPost("/refresh", async (GameDb db, AuthSession sessions, HttpContext ctx) =>
        {
            var raw = ctx.Request.Cookies["bm_refresh"];
            if (raw == null || raw.Length > 200) return Results.Unauthorized();
            var hash = AuthSession.Hash(raw);
            var session = await db.Sessions.SingleOrDefaultAsync(s => s.Hash == hash);
            if (session == null || session.Expires <= DateTime.UtcNow) return Results.Unauthorized();
            var a = await db.Accounts.FindAsync(session.UserId);
            return a == null ? Results.Unauthorized() : await sessions.Issue(a, ctx, session);
        }).RequireRateLimiting("auth");
        auth.MapPost("/logout", async (GameDb db, HttpContext ctx) =>
        {
            var raw = ctx.Request.Cookies["bm_refresh"];
            if (raw is { Length: <= 200 })
            {
                var hash = AuthSession.Hash(raw); var s = await db.Sessions.SingleOrDefaultAsync(s => s.Hash == hash);
                if (s != null) { db.Remove(s); db.Audits.Add(new() { UserId = s.UserId, Action = "auth:logout" }); await db.SaveChangesAsync(); }
            }
            ctx.Response.Cookies.Delete("bm_refresh", new() { Path = "/api/auth" }); return Results.NoContent();
        });
        auth.MapGet("/me", (ClaimsPrincipal u, IConfiguration config) => Results.Ok(new { id = u.FindFirstValue(ClaimTypes.NameIdentifier), name = u.Identity!.Name, isAdmin = AdminAccess.Allowed(config, u) })).RequireAuthorization();
        var account = auth.MapGroup("/sessions").RequireAuthorization().RequireRateLimiting("game");
        account.MapGet("/", async (GameDb db, ClaimsPrincipal u) =>
        {
            var user = Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!); var current = Guid.Parse(u.FindFirstValue("session_id")!);
            return await db.Sessions.Where(s => s.UserId == user && s.Expires > DateTime.UtcNow)
                .OrderByDescending(s => s.Created).Select(s => new { s.Id, s.Created, s.Expires, s.Device, current = s.Id == current }).ToListAsync();
        });
        account.MapPost("/{id:guid}/revoke", async (Guid id, GameDb db, ClaimsPrincipal u) =>
        {
            var user = Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var session = await db.Sessions.SingleOrDefaultAsync(s => s.UserId == user && s.Id == id);
            if (session == null) return Results.NotFound();
            db.Remove(session); db.Audits.Add(new() { UserId = user, Action = "auth:revoke-session:" + id });
            await db.SaveChangesAsync(); return Results.NoContent();
        });
    }
}
public sealed record Credentials(string Email, string Password, string Name = "Treinador", bool Remember = true);

public sealed class SessionCleanup(IServiceScopeFactory scopes, ILogger<SessionCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<GameDb>();
                    var expired = await db.Sessions.Where(s => s.Expires < DateTime.UtcNow).Take(1000).ToListAsync(stoppingToken);
                    db.RemoveRange(expired); await db.SaveChangesAsync(stoppingToken);
                }
                catch (Exception e) when (!stoppingToken.IsCancellationRequested) { logger.LogError(e, "Falha ao remover sessões expiradas."); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}

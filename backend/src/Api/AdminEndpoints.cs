using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Brasileirao;

// Painel do administrador: contas, clubes, jogadores e escudos.
// Alterações de clubes e jogadores gravam no catálogo. Carreiras já criadas guardam uma cópia própria dos dados
// e não mudam; só as carreiras novas recebem os valores editados.
public static class AdminEndpoints
{
    static readonly Regex LogoPattern = new(@"^data:image/(png|jpeg|webp);base64,[A-Za-z0-9+/]+={0,2}$", RegexOptions.Compiled);
    const int MaxLogoChars = 280_000;

    static Guid Me(ClaimsPrincipal u) => Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!);
    static void Audit(GameDb db, ClaimsPrincipal u, string action) => db.Audits.Add(new() { UserId = Me(u), Action = action });
    static IResult Bad(string message) => Results.BadRequest(new { message });

    public static void MapAdminEndpoints(this WebApplication app)
    {
        // Escudos enviados: lista pública e imagem. O parâmetro v muda quando o escudo muda.
        app.MapGet("/api/logos", async (GameDb db) =>
        {
            var rows = await db.ClubData.AsNoTracking().Where(c => c.Logo != "").Select(c => new { c.Id, c.Logo }).ToListAsync();
            return rows.GroupBy(r => r.Id).Select(g => new { clubId = g.Key, v = AuthSession.Hash(g.First().Logo)[..12] });
        });
        app.MapGet("/api/logos/{clubId}", async (string clubId, GameDb db, HttpContext ctx) =>
        {
            var logo = await db.ClubData.AsNoTracking().Where(c => c.Id == clubId && c.Logo != "").Select(c => c.Logo).FirstOrDefaultAsync();
            if (logo == null) return Results.NotFound();
            var comma = logo.IndexOf(',');
            var type = logo[5..logo.IndexOf(';')];
            ctx.Response.Headers.CacheControl = "public, max-age=60";
            return Results.File(Convert.FromBase64String(logo[(comma + 1)..]), type);
        });

        var admin = app.MapGroup("/api/admin").RequireAuthorization("Admin").RequireRateLimiting("game");

        // ---- Contas ----
        admin.MapGet("/accounts", async (string? q, GameDb db, IConfiguration config) =>
        {
            var query = db.Accounts.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim().ToLowerInvariant();
                query = query.Where(a => a.Email.ToLower().Contains(term) || a.Name.ToLower().Contains(term));
            }
            var accounts = await query.OrderBy(a => a.Name).Take(200).ToListAsync();
            var ids = accounts.Select(a => a.Id).ToList();
            var careers = await db.Careers.Where(c => ids.Contains(c.UserId)).GroupBy(c => c.UserId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
            var sessions = await db.Sessions.Where(s => ids.Contains(s.UserId) && s.Expires > DateTime.UtcNow).GroupBy(s => s.UserId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
            var logins = await db.Audits.Where(a => ids.Contains(a.UserId) && (a.Action == "auth:login" || a.Action == "auth:register"))
                .GroupBy(a => a.UserId).Select(g => new { g.Key, At = g.Max(a => a.At) }).ToDictionaryAsync(x => x.Key, x => x.At);
            var admins = (config["Admin:UserIds"] ?? "").Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return accounts.Select(a => new
            {
                a.Id, a.Name, a.Email,
                careers = careers.GetValueOrDefault(a.Id), sessions = sessions.GetValueOrDefault(a.Id),
                lastAccess = logins.TryGetValue(a.Id, out var at) ? at : (DateTime?)null,
                isAdmin = admins.Any(s => string.Equals(s, a.Email, StringComparison.OrdinalIgnoreCase) || s == a.Id.ToString())
            });
        });
        admin.MapPost("/accounts/{id:guid}/rename", async (Guid id, RenameRequest r, GameDb db, ClaimsPrincipal u) =>
        {
            var name = (r.Name ?? "").Trim();
            if (name.Length is < 1 or > 60) return Bad("O nome deve ter de 1 a 60 caracteres.");
            var a = await db.Accounts.FindAsync(id); if (a == null) return Results.NotFound();
            a.Name = name; Audit(db, u, "admin:account-rename:" + id);
            await db.SaveChangesAsync(); return Results.NoContent();
        });
        admin.MapPost("/accounts/{id:guid}/reset-password", async (Guid id, PasswordRequest r, GameDb db, ClaimsPrincipal u) =>
        {
            if (string.IsNullOrEmpty(r.Password) || r.Password.Length is < 12 or > 128) return Bad("A senha deve ter de 12 a 128 caracteres.");
            var a = await db.Accounts.FindAsync(id); if (a == null) return Results.NotFound();
            a.PasswordHash = new PasswordHasher<Account>().HashPassword(a, r.Password);
            db.Sessions.RemoveRange(db.Sessions.Where(s => s.UserId == id));
            Audit(db, u, "admin:account-reset-password:" + id);
            await db.SaveChangesAsync(); return Results.NoContent();
        });
        admin.MapPost("/accounts/{id:guid}/revoke-sessions", async (Guid id, GameDb db, ClaimsPrincipal u) =>
        {
            if (!await db.Accounts.AnyAsync(a => a.Id == id)) return Results.NotFound();
            db.Sessions.RemoveRange(db.Sessions.Where(s => s.UserId == id));
            Audit(db, u, "admin:account-revoke-sessions:" + id);
            await db.SaveChangesAsync(); return Results.NoContent();
        });
        admin.MapDelete("/accounts/{id:guid}", async (Guid id, GameDb db, ClaimsPrincipal u) =>
        {
            if (id == Me(u)) return Results.Conflict(new { message = "Você não pode apagar a própria conta." });
            var a = await db.Accounts.FindAsync(id); if (a == null) return Results.NotFound();
            db.Careers.RemoveRange(db.Careers.Where(c => c.UserId == id));
            db.Sessions.RemoveRange(db.Sessions.Where(s => s.UserId == id));
            db.Remove(a); Audit(db, u, "admin:account-delete:" + id);
            await db.SaveChangesAsync(); return Results.NoContent();
        });

        // ---- Clubes e jogadores de uma versão do catálogo ----
        admin.MapPost("/releases/{rid:guid}/clubs/{clubId}", async (Guid rid, string clubId, ClubEdit r, GameDb db, ClaimsPrincipal u) =>
            await Edit(db, rid, u, $"club-edit:{clubId}", d =>
            {
                var i = d.Clubs.FindIndex(c => c.Id == clubId);
                if (i < 0) return Results.NotFound();
                var c = d.Clubs[i];
                d.Clubs[i] = new Club(c.Id, r.Name?.Trim() ?? c.Name, r.Short?.Trim().ToUpperInvariant() ?? c.Short, r.Color?.Trim() ?? c.Color, r.Strength ?? c.Strength);
                return null;
            }));
        admin.MapPost("/releases/{rid:guid}/players", async (Guid rid, PlayerCreate r, GameDb db, ClaimsPrincipal u) =>
        {
            int created = 0;
            return await Edit(db, rid, u, "player-add", d =>
            {
                if (r.ClubId != "" && !d.Clubs.Any(c => c.Id == r.ClubId)) return Bad("Clube inexistente nesta versão.");
                created = (d.Players.Count == 0 ? 0 : d.Players.Max(p => p.Player.Id)) + 1;
                d.Players.Add(new CatalogPlayer(r.ClubId, new Player { Id = created, Name = (r.Name ?? "").Trim(), Position = r.Position ?? "", Age = r.Age, Rating = r.Rating, Value = r.Value, Wage = r.Wage }));
                return null;
            }, () => new { id = created });
        });
        admin.MapPost("/releases/{rid:guid}/players/{pid:int}", async (Guid rid, int pid, PlayerEdit r, GameDb db, ClaimsPrincipal u) =>
            await Edit(db, rid, u, $"player-edit:{pid}", d =>
            {
                var i = d.Players.FindIndex(p => p.Player.Id == pid);
                if (i < 0) return Results.NotFound();
                var e = d.Players[i]; var p = e.Player;
                if (r.Name != null) p.Name = r.Name.Trim();
                if (r.Position != null) p.Position = r.Position;
                if (r.Age != null) p.Age = r.Age.Value;
                if (r.Rating != null) p.Rating = r.Rating.Value;
                if (r.Value != null) p.Value = r.Value.Value;
                if (r.Wage != null) p.Wage = r.Wage.Value;
                if (r.ClubId != null) d.Players[i] = new CatalogPlayer(r.ClubId, p);
                return null;
            }));
        admin.MapDelete("/releases/{rid:guid}/players/{pid:int}", async (Guid rid, int pid, GameDb db, ClaimsPrincipal u) =>
            await Edit(db, rid, u, $"player-delete:{pid}", d => d.Players.RemoveAll(p => p.Player.Id == pid) == 0 ? Results.NotFound() : null));

        // ---- Escudo do clube (vale para todas as versões que têm o clube) ----
        admin.MapPost("/clubs/{clubId}/logo", async (string clubId, LogoRequest r, GameDb db, ClaimsPrincipal u) =>
        {
            var data = r.DataUri ?? "";
            if (data.Length > MaxLogoChars) return Bad("O escudo deve ter no máximo cerca de 200 KB.");
            if (!LogoPattern.IsMatch(data)) return Bad("Envie uma imagem PNG, JPEG ou WebP.");
            try { Convert.FromBase64String(data[(data.IndexOf(',') + 1)..]); } catch (FormatException) { return Bad("Imagem inválida."); }
            var rows = await db.ClubData.Where(c => c.Id == clubId).ToListAsync();
            if (rows.Count == 0) return Results.NotFound();
            foreach (var row in rows) row.Logo = data;
            Audit(db, u, "admin:logo-set:" + clubId); await db.SaveChangesAsync(); return Results.NoContent();
        });
        admin.MapDelete("/clubs/{clubId}/logo", async (string clubId, GameDb db, ClaimsPrincipal u) =>
        {
            var rows = await db.ClubData.Where(c => c.Id == clubId).ToListAsync();
            if (rows.Count == 0) return Results.NotFound();
            foreach (var row in rows) row.Logo = "";
            Audit(db, u, "admin:logo-clear:" + clubId); await db.SaveChangesAsync(); return Results.NoContent();
        });
    }

    // Lê o catálogo, aplica a alteração, valida o conjunto e grava. Se qualquer regra falhar nada é salvo.
    static async Task<IResult> Edit(GameDb db, Guid rid, ClaimsPrincipal u, string action, Func<SeasonCatalog, IResult?> change, Func<object>? payload = null)
    {
        if (!await db.DataReleases.AnyAsync(r => r.Id == rid)) return Results.NotFound();
        var store = new CatalogStore(db);
        var catalog = await store.Read(rid);
        var failure = change(catalog);
        if (failure != null) return failure;
        CatalogImport.Validate(catalog);
        await store.ReplaceRows(rid, catalog);
        Audit(db, u, $"admin:{action}");
        await db.SaveChangesAsync();
        return Results.Ok(payload?.Invoke() ?? new { ok = true });
    }
}

public sealed record RenameRequest(string Name);
public sealed record PasswordRequest(string Password);
public sealed record ClubEdit(string? Name, string? Short, string? Color, int? Strength);
public sealed record PlayerCreate(string ClubId, string Name, string Position, int Age, int Rating, int Value, int Wage);
public sealed record PlayerEdit(string? Name, string? Position, int? Age, int? Rating, int? Value, int? Wage, string? ClubId);
public sealed record LogoRequest(string DataUri);

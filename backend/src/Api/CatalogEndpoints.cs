using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Brasileirao;

public static class CatalogEndpoints
{
    public static async Task SeedCatalog(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var existing = await db.DataReleases.Select(release => release.Version).ToListAsync();
        var catalogs = new List<SeasonCatalog>();
        if (existing.Count == 0) catalogs.Add(Game.DemoCatalog());
        if (!existing.Contains(Game.Clubs2026Version)) catalogs.Add(Game.Clubs2026Catalog());
        if (!existing.Contains(Game.Candidate2026Version)) catalogs.Add(Game.Candidate2026Catalog());
        foreach (var data in catalogs)
        {
            CatalogImport.Validate(data);
            var release = new DataRelease
            {
                Version = data.Version, Season = data.Season, Source = data.Source, License = data.License,
                Published = true, PublishedAt = DateTime.UtcNow
            };
            db.Add(release);
            new CatalogStore(db).AddRows(release.Id, data);
        }
        await db.SaveChangesAsync();
    }
    public static async Task<DataRelease?> ChooseRelease(GameDb db, Guid? id) => await db.DataReleases.AsNoTracking()
        .Where(r => r.Published && (id == null || r.Id == id)).OrderByDescending(r => r.PublishedAt).FirstOrDefaultAsync();

    public static void MapCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/catalogs", async (GameDb db) => await db.DataReleases.AsNoTracking().Where(r => r.Published).OrderByDescending(r => r.PublishedAt).ToListAsync());
        app.MapGet("/api/clubs", async (Guid? releaseId, GameDb db) =>
        {
            var release = await ChooseRelease(db, releaseId);
            return release == null ? Results.NotFound() : Results.Ok((await new CatalogStore(db).Read(release.Id)).Clubs);
        });
        var admin = app.MapGroup("/api/admin/catalogs").RequireAuthorization("Admin").RequireRateLimiting("game");
        admin.MapGet("/", async (GameDb db) => await db.DataReleases.AsNoTracking().OrderByDescending(r => r.Created).ToListAsync());
        admin.MapGet("/template", () => Results.Ok(new { json = JsonSerializer.Serialize(Game.DemoCatalog(), new JsonSerializerOptions(Game.Json) { WriteIndented = true }), csv = CatalogImport.ToCsv(Game.DemoCatalog()) }));
        admin.MapGet("/{id:guid}", async (Guid id, GameDb db) => await db.DataReleases.AnyAsync(r => r.Id == id)
            ? Results.Ok(new { release = await db.DataReleases.AsNoTracking().SingleAsync(r => r.Id == id), catalog = await new CatalogStore(db).Read(id) }) : Results.NotFound());
        admin.MapPost("/validate", (ImportRequest request) =>
        {
            var d = CatalogImport.Parse(request.Format, request.Content, request.Version, request.Season, request.Source, request.License);
            return Results.Ok(new { clubs = d.Clubs.Count, players = d.Players.Count, message = "Dados válidos. Ainda não foram salvos ou publicados." });
        });
        admin.MapPost("/", async (ImportRequest request, GameDb db, ClaimsPrincipal user) =>
        {
            var d = CatalogImport.Parse(request.Format, request.Content, request.Version, request.Season, request.Source, request.License);
            if (await db.DataReleases.AnyAsync(r => r.Version == d.Version)) return Results.Conflict(new { message = "Essa versão já existe. Use um identificador novo." });
            var release = new DataRelease { Version = d.Version, Season = d.Season, Source = d.Source, License = d.License };
            db.Add(release); new CatalogStore(db).AddRows(release.Id, d);
            AddAudit(db, user, "catalog:create:" + d.Version);
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { return Results.Conflict(new { message = "A versão foi criada por outra sessão. Recarregue." }); }
            return Results.Ok(release);
        });
        admin.MapPost("/{id:guid}/edit", async (Guid id, ImportRequest request, GameDb db, ClaimsPrincipal user) =>
        {
            var release = await db.DataReleases.SingleOrDefaultAsync(r => r.Id == id);
            if (release == null) return Results.NotFound();
            if (release.Published) return Results.Conflict(new { message = "Versões publicadas são imutáveis. Crie uma nova versão." });
            if (request.Revision != release.Revision) return Results.Conflict(new { message = "Rascunho alterado por outra sessão. Reabra antes de editar." });
            var d = CatalogImport.Parse(request.Format, request.Content, release.Version, request.Season, request.Source, request.License);
            release.Season = d.Season; release.Source = d.Source; release.License = d.License; release.Revision++;
            await new CatalogStore(db).ReplaceRows(id, d); AddAudit(db, user, "catalog:edit:" + release.Version);
            await db.SaveChangesAsync(); return Results.Ok(release);
        });
        admin.MapPost("/{id:guid}/publish", async (Guid id, PublishRequest request, GameDb db, ClaimsPrincipal user) =>
        {
            var release = await db.DataReleases.SingleOrDefaultAsync(r => r.Id == id);
            if (release == null) return Results.NotFound();
            if (release.Published || release.Revision != request.Revision) return Results.Conflict(new { message = "Versão já publicada ou rascunho desatualizado." });
            CatalogImport.Validate(await new CatalogStore(db).Read(id));
            release.Published = true; release.PublishedAt = DateTime.UtcNow; release.Revision++;
            AddAudit(db, user, "catalog:publish:" + release.Version);
            await db.SaveChangesAsync(); return Results.Ok(release);
        });
    }
    static void AddAudit(GameDb db, ClaimsPrincipal user, string action) => db.Audits.Add(new() { UserId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = action });
}
public sealed record ImportRequest(string Format, string Content, string Version, int Season, string Source, string License, int Revision = 0);
public sealed record PublishRequest(int Revision);

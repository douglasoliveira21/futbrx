using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Brasileirao;

// Séries A, B e C têm sempre vinte clubes. A série de um clube é a das versões de dados em que ele aparece:
// trocar de série move o clube entre as versões e substituir tira um clube da série (ele passa à Série D, que não é jogável).
public static class AdminSeriesEndpoints
{
    static readonly string[] Playable = ["A", "B", "C"];
    static IResult Bad(string message) => Results.BadRequest(new { message });

    sealed class Slot
    {
        public required DataRelease Release;
        public required string Series;
        public required SeasonCatalog Catalog;
        public Dictionary<string, (string City, string Uf, string Logo)> Extras = [];
        public HashSet<string> Touched = [];
    }
    sealed record Source(Club Club, List<Player> Players, (string City, string Uf, string Logo) Extra);

    static async Task<List<Slot>> Load(GameDb db)
    {
        var store = new CatalogStore(db);
        var releases = (await db.DataReleases.AsNoTracking().Where(r => r.Published).ToListAsync())
            .Select(r => (Release: r, Series: Game.SeriesOfVersion(r.Version))).Where(x => x.Series != null)
            .OrderBy(x => x.Series).ThenByDescending(x => x.Release.PublishedAt).ToList();
        var slots = new List<Slot>();
        foreach (var (release, series) in releases)
        {
            var extras = (await db.ClubData.AsNoTracking().Where(c => c.ReleaseId == release.Id).Select(c => new { c.Id, c.City, c.Uf, c.Logo }).ToListAsync())
                .ToDictionary(c => c.Id, c => (c.City, c.Uf, c.Logo));
            slots.Add(new Slot { Release = release, Series = series!, Catalog = await store.Read(release.Id), Extras = extras });
        }
        return slots;
    }
    static string? SeriesOf(List<Slot> slots, string clubId) => slots.FirstOrDefault(s => s.Catalog.Clubs.Any(c => c.Id == clubId))?.Series;

    static Source? Take(List<Slot> slots, string series, string clubId)
    {
        var slot = slots.FirstOrDefault(s => s.Series == series && s.Catalog.Clubs.Any(c => c.Id == clubId));
        if (slot == null) return null;
        var players = slot.Catalog.Players.Where(p => p.ClubId == clubId).Select(p => Clone(p.Player)).ToList();
        return new(slot.Catalog.Clubs.First(c => c.Id == clubId), players, slot.Extras.GetValueOrDefault(clubId, ("", "", "")));
    }
    static Player Clone(Player p) => new()
    {
        Id = p.Id, Name = p.Name, Position = p.Position, Age = p.Age, Rating = p.Rating, Value = p.Value, Wage = p.Wage,
        PreferredFoot = p.PreferredFoot, Fitness = p.Fitness, Starter = p.Starter
    };
    static void Remove(Slot slot, string clubId)
    {
        slot.Catalog.Clubs.RemoveAll(c => c.Id == clubId);
        slot.Catalog.Players.RemoveAll(p => p.ClubId == clubId);
        slot.Extras.Remove(clubId);
        slot.Touched.Add(clubId);
    }
    // Os jogadores recebem IDs livres dentro da versão (os IDs só precisam ser únicos em cada versão).
    static void Add(Slot slot, Source source)
    {
        var used = slot.Catalog.Players.Select(p => p.Player.Id).ToHashSet();
        var next = 0;
        slot.Catalog.Clubs.Add(source.Club);
        foreach (var p in source.Players)
        {
            while (used.Contains(next)) next++;
            p.Id = next; used.Add(next);
            slot.Catalog.Players.Add(new CatalogPlayer(source.Club.Id, p));
        }
        slot.Extras[source.Club.Id] = source.Extra;
        slot.Touched.Add(source.Club.Id);
    }
    static async Task Save(GameDb db, List<Slot> slots)
    {
        foreach (var slot in slots.Where(s => s.Touched.Count > 0)) CatalogImport.Validate(slot.Catalog);
        var store = new CatalogStore(db);
        foreach (var slot in slots.Where(s => s.Touched.Count > 0))
        {
            await store.ReplaceRows(slot.Release.Id, slot.Catalog);
            foreach (var entry in db.ChangeTracker.Entries<ClubData>().Where(e => e.Entity.ReleaseId == slot.Release.Id && e.State == EntityState.Added))
            {
                var (city, uf, logo) = slot.Extras.GetValueOrDefault(entry.Entity.Id, ("", "", ""));
                entry.Entity.City = city; entry.Entity.Uf = uf; entry.Entity.Logo = logo;
            }
        }
    }

    public static string Slug(string name)
    {
        var plain = new string(name.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
        var slug = Regex.Replace(plain, "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 20 ? slug[..20].Trim('-') : slug;
    }

    // Diretório público: a série, o nome e as cores vêm das versões de dados, para refletir as edições do administrador.
    public static async Task<string> BuildDirectory(GameDb db)
    {
        var node = JsonNode.Parse(Game.DirectoryJson())!.AsObject();
        var clubs = node["clubs"]!.AsArray();
        var slots = await Load(db);
        var known = new Dictionary<string, (string Series, Club Club, (string City, string Uf, string Logo) Extra)>();
        foreach (var slot in slots)
            foreach (var club in slot.Catalog.Clubs)
                known.TryAdd(club.Id, (slot.Series, club, slot.Extras.GetValueOrDefault(club.Id, ("", "", ""))));
        var seen = new HashSet<string>();
        foreach (var entry in clubs.OfType<JsonObject>())
        {
            var id = entry["id"]!.GetValue<string>(); seen.Add(id);
            if (known.TryGetValue(id, out var k))
            {
                entry["series"] = k.Series; entry["name"] = k.Club.Name; entry["short"] = k.Club.Short;
                entry["color"] = k.Club.Color; entry["strength"] = k.Club.Strength;
            }
            else if (entry["series"]!.GetValue<string>() != "D") entry["series"] = "D";
        }
        foreach (var (id, k) in known.Where(x => !seen.Contains(x.Key)))
            clubs.Add(new JsonObject
            {
                ["id"] = id, ["series"] = k.Series, ["name"] = k.Club.Name, ["city"] = k.Extra.City, ["uf"] = k.Extra.Uf,
                ["short"] = k.Club.Short, ["color"] = k.Club.Color, ["strength"] = k.Club.Strength
            });
        return node.ToJsonString();
    }

    public static void MapAdminSeriesEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapPost("/clubs/swap", async (SwapRequest r, GameDb db, ClaimsPrincipal u) =>
        {
            var slots = await Load(db);
            var sa = SeriesOf(slots, r.ClubA ?? ""); var sb = SeriesOf(slots, r.ClubB ?? "");
            if (sa == null || sb == null) return Bad("Escolha dois clubes das Séries A, B ou C.");
            if (sa == sb) return Bad("Os dois clubes já estão na mesma série.");
            var a = Take(slots, sa, r.ClubA!); var b = Take(slots, sb, r.ClubB!);
            foreach (var slot in slots)
            {
                if (slot.Series == sa) { Remove(slot, a!.Club.Id); Add(slot, new Source(b!.Club, b.Players.Select(Clone).ToList(), b.Extra)); }
                else if (slot.Series == sb) { Remove(slot, b!.Club.Id); Add(slot, new Source(a!.Club, a.Players.Select(Clone).ToList(), a.Extra)); }
            }
            await Save(db, slots);
            db.Audits.Add(new() { UserId = Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = $"admin:series-swap:{r.ClubA}:{r.ClubB}" });
            await db.SaveChangesAsync();
            return Results.Ok(new { ok = true });
        });

        admin.MapPost("/series/{series}/replace", async (string series, ReplaceRequest r, GameDb db, ClaimsPrincipal u) =>
        {
            series = series.ToUpperInvariant();
            if (!Playable.Contains(series)) return Bad("Série inválida.");
            var slots = await Load(db);
            var outgoing = Take(slots, series, r.OutClubId ?? "");
            if (outgoing == null) return Bad("O time que sai precisa estar nesta série.");
            var name = (r.Name ?? "").Trim();
            var id = string.IsNullOrWhiteSpace(r.Id) ? Slug(name) : r.Id.Trim();
            if (SeriesOf(slots, id) != null) return Bad("Já existe um time com esse ID em uma série. Use \"Trocar de série\" para mover times existentes.");
            var city = (r.City ?? "").Trim(); var uf = (r.Uf ?? "").Trim().ToUpperInvariant();
            if (city.Length is < 1 or > 60 || !Regex.IsMatch(uf, "^[A-Z]{2}$")) return Bad("Informe a cidade e o estado (sigla de duas letras).");
            var strength = r.Strength is >= 1 and <= 99 ? r.Strength : 70;
            var club = new Club(id, name, (r.Short ?? "").Trim().ToUpperInvariant(), (r.Color ?? "#888888").Trim(), strength);
            foreach (var slot in slots.Where(s => s.Series == series))
            {
                var squad = Game.Players(0);
                foreach (var p in squad) p.Rating = Math.Clamp(p.Rating + (strength - 75) / 2, 1, 99);
                Remove(slot, outgoing.Club.Id);
                Add(slot, new Source(club, squad, (city, uf, "")));
            }
            await Save(db, slots);
            db.Audits.Add(new() { UserId = Guid.Parse(u.FindFirstValue(ClaimTypes.NameIdentifier)!), Action = $"admin:series-replace:{series}:{outgoing.Club.Id}:{id}" });
            await db.SaveChangesAsync();
            return Results.Ok(new { id });
        });
    }
}

public sealed record SwapRequest(string ClubA, string ClubB);
public sealed record ReplaceRequest(string OutClubId, string? Id, string Name, string Short, string Color, int Strength, string City, string Uf);

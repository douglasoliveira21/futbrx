using Microsoft.EntityFrameworkCore;

namespace Brasileirao;

public sealed class CatalogStore(GameDb db)
{
    public async Task<SeasonCatalog> Read(Guid id)
    {
        var release = await db.DataReleases.AsNoTracking().SingleAsync(r => r.Id == id);
        var clubs = await db.ClubData.AsNoTracking().Where(c => c.ReleaseId == id).OrderBy(c => c.Id).ToListAsync();
        var players = await db.PlayerData.AsNoTracking().Where(p => p.ReleaseId == id).OrderBy(p => p.Id).ToListAsync();
        return new()
        {
            Version = release.Version,
            Season = release.Season,
            Source = release.Source,
            License = release.License,
            Clubs = clubs.Select(c => new Club(c.Id, c.Name, c.Short, c.Color, c.Strength)).ToList(),
            Players = players.Select(p => new CatalogPlayer(p.ClubId, new() { Id = p.Id, Name = p.Name, Position = p.Position, Age = p.Age, Rating = p.Rating, Value = p.Value, Wage = p.Wage })).ToList()
        };
    }
    public void AddRows(Guid id, SeasonCatalog d)
    {
        db.ClubData.AddRange(d.Clubs.Select(c => new ClubData { ReleaseId = id, Id = c.Id, Name = c.Name, Short = c.Short, Color = c.Color, Strength = c.Strength }));
        db.PlayerData.AddRange(d.Players.Select(e => new PlayerData { ReleaseId = id, Id = e.Player.Id, ClubId = e.ClubId, Name = e.Player.Name, Position = e.Player.Position, Age = e.Player.Age, Rating = e.Player.Rating, Value = e.Player.Value, Wage = e.Player.Wage }));
    }
    // A versão é imutável. Edição de um rascunho exige a revisão corrente.
    public async Task ReplaceRows(Guid id, SeasonCatalog d)
    {
        var clubs = await db.ClubData.Where(c => c.ReleaseId == id).ToListAsync();
        var players = await db.PlayerData.Where(p => p.ReleaseId == id).ToListAsync();
        foreach (var old in clubs)
        {
            var c = d.Clubs.Find(c => c.Id == old.Id);
            if (c == null) db.Remove(old);
            else { old.Name = c.Name; old.Short = c.Short; old.Color = c.Color; old.Strength = c.Strength; }
        }
        db.ClubData.AddRange(d.Clubs.Where(c => !clubs.Any(old => old.Id == c.Id)).Select(c => new ClubData { ReleaseId = id, Id = c.Id, Name = c.Name, Short = c.Short, Color = c.Color, Strength = c.Strength }));
        foreach (var old in players)
        {
            var e = d.Players.Find(p => p.Player.Id == old.Id);
            if (e == null) db.Remove(old);
            else { var p = e.Player; old.ClubId = e.ClubId; old.Name = p.Name; old.Position = p.Position; old.Age = p.Age; old.Rating = p.Rating; old.Value = p.Value; old.Wage = p.Wage; }
        }
        db.PlayerData.AddRange(d.Players.Where(p => !players.Any(old => old.Id == p.Player.Id)).Select(e => new PlayerData { ReleaseId = id, Id = e.Player.Id, ClubId = e.ClubId, Name = e.Player.Name, Position = e.Player.Position, Age = e.Player.Age, Rating = e.Player.Rating, Value = e.Player.Value, Wage = e.Player.Wage }));
    }
}

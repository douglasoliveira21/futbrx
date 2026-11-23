using Microsoft.EntityFrameworkCore;
namespace Brasileirao;

public class GameDb(DbContextOptions<GameDb> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>(); public DbSet<Session> Sessions => Set<Session>(); public DbSet<Career> Careers => Set<Career>(); public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<DataRelease> DataReleases => Set<DataRelease>();
    public DbSet<ClubData> ClubData => Set<ClubData>();
    public DbSet<PlayerData> PlayerData => Set<PlayerData>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Account>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Session>().HasIndex(x => x.Hash).IsUnique();
        b.Entity<Session>().Property(x => x.Hash).IsConcurrencyToken();
        b.Entity<Career>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<Career>().HasIndex(x => x.UserId);
        b.Entity<DataRelease>().HasIndex(x => x.Version).IsUnique();
        b.Entity<DataRelease>().Property(x => x.Revision).IsConcurrencyToken();
        b.Entity<ClubData>().HasKey(x => new { x.ReleaseId, x.Id });
        b.Entity<PlayerData>().HasKey(x => new { x.ReleaseId, x.Id });
        b.Entity<ClubData>().HasOne<DataRelease>().WithMany().HasForeignKey(x => x.ReleaseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PlayerData>().HasOne<DataRelease>().WithMany().HasForeignKey(x => x.ReleaseId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<PlayerData>().HasIndex(x => new { x.ReleaseId, x.ClubId });
    }
}


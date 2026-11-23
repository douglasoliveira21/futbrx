using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Brasileirao;
// Geração de migrações não precisa iniciar a API nem conectar ao banco.
public class DesignTimeFactory : IDesignTimeDbContextFactory<GameDb>
{
    public GameDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<GameDb>().UseNpgsql("Host=localhost;Database=brasileirao;Username=manager;Password=design-time-only", o => o.MigrationsAssembly("Api")).Options);
}

using Cemetery.Infrastructure.Persistence;
using Cemetery.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Cemetery.Infrastructure.Persistence;

public sealed class CemeteryDbContextFactory : IDesignTimeDbContextFactory<CemeteryDbContext>
{
    public CemeteryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CemeteryDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=cemetery;Username=postgres;Password=postgres",
                npgsql => npgsql.UseNetTopologySuite().MigrationsHistoryTable("__ef_migrations", "cemetery"))
            .UseSnakeCaseNamingConvention()
            .Options;
        return new CemeteryDbContext(options, new AmbientTenant());
    }
}

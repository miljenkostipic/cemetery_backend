using Cemetery.Infrastructure;
using Cemetery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Cemetery.Architecture.Tests;

public sealed class MigrationHistoryTests : IClassFixture<CemeteryApiFactory>
{
    private readonly CemeteryApiFactory _factory;

    public MigrationHistoryTests(CemeteryApiFactory factory) => _factory = factory;

    [Fact]
    public void Startup_and_the_runtime_context_share_the_cemetery_history_table()
    {
        AssertHistory(DatabaseInitializer.CreateOptions("Host=localhost;Database=cemetery"));
        using var scope = _factory.Services.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<DbContextOptions<CemeteryDbContext>>();
        AssertHistory(runtime);
    }

    private static void AssertHistory(DbContextOptions options)
    {
        var relational = options.Extensions.OfType<RelationalOptionsExtension>().Single();
        Assert.Equal("__ef_migrations", relational.MigrationsHistoryTableName);
        Assert.Equal("cemetery", relational.MigrationsHistoryTableSchema);
    }
}

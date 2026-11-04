using Cemetery.Infrastructure;
using Cemetery.Infrastructure.Persistence;
using Cemetery.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cemetery.Architecture.Tests;

/// <summary>
/// The design-time factory, the startup migrator and the running app must build the same model.
/// The first two once had no Identity options, so Identity modelled schema version 1: the
/// migrations never created AspNetUserPasskeys and every passkey request failed with a 500.
/// </summary>
public sealed class IdentityModelTests : IClassFixture<CemeteryApiFactory>
{
    private readonly CemeteryApiFactory _factory;

    public IdentityModelTests(CemeteryApiFactory factory) => _factory = factory;

    [Fact]
    public void Every_context_models_passkeys_and_matches_the_migrations()
    {
        using var designTime = new CemeteryDbContextFactory().CreateDbContext([]);
        using var startup = new CemeteryDbContext(DatabaseInitializer.CreateOptions("Host=localhost;Database=cemetery"), new AmbientTenant());
        using var scope = _factory.Services.CreateScope();
        var runtime = scope.ServiceProvider.GetRequiredService<CemeteryDbContext>();

        foreach (var context in new[] { designTime, startup, runtime })
        {
            Assert.NotNull(context.Model.FindEntityType(typeof(IdentityUserPasskey<Guid>)));
            Assert.False(context.Database.HasPendingModelChanges(), "the model has changes no migration records");
        }
    }
}

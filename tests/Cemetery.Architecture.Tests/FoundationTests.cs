using Cemetery.Api;
using Cemetery.Application.Organizations;
using Cemetery.Domain.Identity;
using Cemetery.Infrastructure.Identity;
using Cemetery.Infrastructure.Persistence;
using Cemetery.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Cemetery.Architecture.Tests;

public sealed class LayerTests
{
    [Fact]
    public void Domain_has_no_project_references()
    {
        var names = typeof(Organization).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name);

        Assert.DoesNotContain(names, name => name?.StartsWith("Cemetery.", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Application_does_not_reference_infrastructure_or_api()
    {
        var names = typeof(CreateOrganizationHandler).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name).ToArray();

        Assert.DoesNotContain("Cemetery.Infrastructure", names);
        Assert.DoesNotContain("Cemetery.Api", names);
    }

    [Fact]
    public void Infrastructure_does_not_reference_the_api()
    {
        var names = typeof(CemeteryDbContext).Assembly.GetReferencedAssemblies().Select(assembly => assembly.Name);

        Assert.DoesNotContain("Cemetery.Api", names);
    }
}

public sealed class TenancyModelTests
{
    [Fact]
    public void Tenant_entities_have_query_filters()
    {
        var options = new DbContextOptionsBuilder<CemeteryDbContext>()
            .UseNpgsql("Host=localhost;Database=cemetery;Username=cemetery_app;Password=cemetery_app", npgsql => npgsql.UseNetTopologySuite())
            .Options;
        using var db = new CemeteryDbContext(options, new AmbientTenant());

        foreach (var entity in db.Model.GetEntityTypes().Where(entity => typeof(TenantEntity).IsAssignableFrom(entity.ClrType)))
            Assert.NotEmpty(entity.GetDeclaredQueryFilters());

        Assert.NotEmpty(db.Model.FindEntityType(typeof(Organization))!.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Row_level_security_covers_tenant_tables()
    {
        Assert.Contains("ENABLE ROW LEVEL SECURITY", RowLevelSecurity.Up, StringComparison.Ordinal);
        Assert.Contains("cemetery.memberships", RowLevelSecurity.Up, StringComparison.Ordinal);
        Assert.Contains("cemetery.invitations", RowLevelSecurity.Up, StringComparison.Ordinal);
        Assert.Contains("cemetery.organizations", RowLevelSecurity.Up, StringComparison.Ordinal);
        Assert.Contains("cemetery.cemeteries", RowLevelSecurity.Layout, StringComparison.Ordinal);
        Assert.Contains("cemetery.sections", RowLevelSecurity.Layout, StringComparison.Ordinal);
        Assert.Contains("cemetery.grave_rows", RowLevelSecurity.Layout, StringComparison.Ordinal);
        Assert.Contains("cemetery.grave_sites", RowLevelSecurity.Layout, StringComparison.Ordinal);
        Assert.Contains("cemetery.deceased", RowLevelSecurity.Register, StringComparison.Ordinal);
        Assert.Contains("cemetery.deceased_revisions", RowLevelSecurity.Register, StringComparison.Ordinal);
        Assert.Contains("cemetery.interments", RowLevelSecurity.Register, StringComparison.Ordinal);
        Assert.Contains("cemetery.audit_entries", RowLevelSecurity.Register, StringComparison.Ordinal);
        Assert.Contains("cemetery.office_requests", RowLevelSecurity.Requests, StringComparison.Ordinal);
        Assert.Contains("cemetery.funerals", RowLevelSecurity.Operations, StringComparison.Ordinal);
        Assert.Contains("cemetery.chapel_bookings", RowLevelSecurity.Operations, StringComparison.Ordinal);
        Assert.Contains("cemetery.work_orders", RowLevelSecurity.Operations, StringComparison.Ordinal);
        Assert.Contains("cemetery.monument_permits", RowLevelSecurity.Operations, StringComparison.Ordinal);
        Assert.Contains("cemetery.work_order_photos", RowLevelSecurity.Operations, StringComparison.Ordinal);
        Assert.Contains("cemetery.outbox_messages", RowLevelSecurity.Outbox, StringComparison.Ordinal);
        Assert.Contains("cemetery.webhook_subscriptions", RowLevelSecurity.Launch, StringComparison.Ordinal);
        Assert.Contains("cemetery.webhook_deliveries", RowLevelSecurity.Launch, StringComparison.Ordinal);
        Assert.Contains("cemetery.matica_links", RowLevelSecurity.Launch, StringComparison.Ordinal);
        Assert.Contains("cemetery.gdpr_exports", RowLevelSecurity.Launch, StringComparison.Ordinal);
        Assert.Contains("cemetery.fee_schedules", RowLevelSecurity.Billing, StringComparison.Ordinal);
        Assert.Contains("cemetery.charges", RowLevelSecurity.Billing, StringComparison.Ordinal);
        Assert.Contains("cemetery.merchant_accounts", RowLevelSecurity.Billing, StringComparison.Ordinal);
        Assert.Contains("cemetery.payments", RowLevelSecurity.Payments, StringComparison.Ordinal);
        Assert.Contains("cemetery.holders", RowLevelSecurity.Rights, StringComparison.Ordinal);
        Assert.Contains("cemetery.grave_rights", RowLevelSecurity.Rights, StringComparison.Ordinal);
        Assert.Contains("cemetery.grave_right_shares", RowLevelSecurity.Rights, StringComparison.Ordinal);
        Assert.Contains("cemetery.reservations", RowLevelSecurity.Rights, StringComparison.Ordinal);
        Assert.Contains("cemetery.site_documents", RowLevelSecurity.Rights, StringComparison.Ordinal);
        Assert.Contains("cemetery.stored_files", RowLevelSecurity.Rights, StringComparison.Ordinal);
        Assert.Contains("cemetery.entrances", RowLevelSecurity.Rights, StringComparison.Ordinal);
        Assert.Contains("cemetery.paths", RowLevelSecurity.Rights, StringComparison.Ordinal);
        Assert.Contains("cemetery.grave_site_photos", RowLevelSecurity.Photos, StringComparison.Ordinal);
        Assert.Contains("SECURITY DEFINER", PublicCatalogSql.Up, StringComparison.Ordinal);
        Assert.Contains("hidden_from_public", PublicCatalogSql.Up, StringComparison.Ordinal);
        Assert.Contains("LIMIT 50", PublicCatalogSql.Up, StringComparison.Ordinal);
        Assert.Contains("FORCE ROW LEVEL SECURITY", RowLevelSecurity.Up, StringComparison.Ordinal);
        Assert.Contains("ST_Intersection", GraveSiteGeometry.Up, StringComparison.Ordinal);
    }
}

public sealed class HostPolicyTests : IClassFixture<CemeteryApiFactory>
{
    private readonly CemeteryApiFactory _factory;

    public HostPolicyTests(CemeteryApiFactory factory) => _factory = factory;

    [Fact]
    public void Endpoint_policies_match_the_snapshot()
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();
        var actual = endpoints
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/v1", StringComparison.Ordinal) == true)
            .Select(Describe)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

        Assert.Equal(ExpectedPolicies(), actual);
    }

    [Fact]
    public void Identity_uses_the_domain_password_policy_and_passkeys()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.Equal(PasswordPolicy.MinimumLength, options.Password.RequiredLength);
        Assert.False(options.Password.RequireNonAlphanumeric);
        Assert.Equal(IdentitySchemaVersions.Version3, options.Stores.SchemaVersion);
        Assert.Equal(PasswordPolicy.MaxFailedAccessAttempts, options.Lockout.MaxFailedAccessAttempts);
    }

    [Fact]
    public void Runtime_connection_is_the_non_owner_role()
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<CemeteryDbContext>>();
        var relational = options.Extensions.OfType<RelationalOptionsExtension>().Single();
        var username = new NpgsqlConnectionStringBuilder(relational.ConnectionString).Username;

        Assert.Equal("cemetery_app", username);
    }

    private static KeyValuePair<string, string> Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        var method = methods is { Count: > 0 } ? methods[0] : "ANY";
        var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>();
        var policy = policies.Count > 0 ? policies[0].Policy : null;
        var access = anonymous ? "anonymous" : policy ?? "authenticated";
        return new KeyValuePair<string, string>($"{method} {endpoint.RoutePattern.RawText}", access);
    }

    private static Dictionary<string, string> ExpectedPolicies()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "policy-snapshot.txt");
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.Length == 0)
                continue;

            var split = line.LastIndexOf(' ');
            expected[line[..split]] = line[(split + 1)..];
        }

        return expected;
    }
}

public sealed class CemeteryApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:App", "Host=localhost;Database=cemetery;Username=cemetery_app;Password=cemetery_app");
        builder.UseSetting("ConnectionStrings:Owner", "Host=localhost;Database=cemetery;Username=postgres;Password=postgres");
        builder.UseSetting("App:PublicUrl", "http://localhost:5173");
    }
}

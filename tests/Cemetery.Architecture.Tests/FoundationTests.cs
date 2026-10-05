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

    private static Dictionary<string, string> ExpectedPolicies() => new(StringComparer.Ordinal)
    {
        ["GET /api/v1/health"] = "anonymous",
        ["POST /api/v1/auth/register"] = "anonymous",
        ["POST /api/v1/auth/login"] = "anonymous",
        ["POST /api/v1/auth/logout"] = "authenticated",
        ["GET /api/v1/auth/me"] = "authenticated",
        ["POST /api/v1/auth/passkeys/creation-options"] = "authenticated",
        ["POST /api/v1/auth/passkeys"] = "authenticated",
        ["POST /api/v1/auth/passkeys/request-options"] = "anonymous",
        ["POST /api/v1/auth/passkeys/login"] = "anonymous",
        ["POST /api/v1/organizations"] = "authenticated",
        ["GET /api/v1/organizations"] = "authenticated",
        ["POST /api/v1/organizations/switch"] = "authenticated",
        ["POST /api/v1/organizations/invitations"] = "tenant",
        ["POST /api/v1/invitations/accept"] = "authenticated",
        ["GET /api/v1/dashboard"] = "tenant",
        ["POST /api/v1/cemeteries"] = "tenant",
        ["GET /api/v1/cemeteries"] = "tenant",
        ["GET /api/v1/cemeteries/{cemeteryId}"] = "tenant",
        ["POST /api/v1/cemeteries/{cemeteryId}/plan"] = "tenant",
        ["POST /api/v1/cemeteries/{cemeteryId}/sections"] = "tenant",
        ["GET /api/v1/cemeteries/{cemeteryId}/sections"] = "tenant",
        ["DELETE /api/v1/sections/{sectionId}"] = "tenant",
        ["PUT /api/v1/sections/{sectionId}/outline"] = "tenant",
        ["POST /api/v1/sections/{sectionId}/rows"] = "tenant",
        ["GET /api/v1/sections/{sectionId}/rows"] = "tenant",
        ["POST /api/v1/sections/{sectionId}/grave-sites"] = "tenant",
        ["GET /api/v1/cemeteries/{cemeteryId}/grave-sites"] = "tenant",
        ["GET /api/v1/grave-sites/{graveSiteId}"] = "tenant",
        ["PUT /api/v1/grave-sites/{graveSiteId}/outline"] = "tenant",
        ["POST /api/v1/grave-sites/{graveSiteId}/split"] = "tenant",
        ["POST /api/v1/grave-sites/merge"] = "tenant",
        ["POST /api/v1/grave-sites/undo-split"] = "tenant",
        ["POST /api/v1/grave-sites/{graveSiteId}/close"] = "tenant",
        ["POST /api/v1/grave-sites/{graveSiteId}/reopen"] = "tenant",
    };
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

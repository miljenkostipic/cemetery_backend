using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cemetery.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Cemetery.Api.IntegrationTests;

public sealed class WalkingSkeletonTests
{
    [Fact]
    public async Task A_user_cannot_read_another_organizations_dashboard()
    {
        if (!File.Exists(@"\\.\pipe\docker_engine") && !File.Exists("/var/run/docker.sock"))
            Assert.Skip("Docker is not available.");

        var cancellationToken = TestContext.Current.CancellationToken;
        await using var container = new PostgreSqlBuilder("postgis/postgis:17-3.5")
            .WithDatabase("cemetery")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();
        await container.StartAsync(cancellationToken);
        var owner = container.GetConnectionString();
        var app = new NpgsqlConnectionStringBuilder(owner) { Username = "cemetery_app", Password = "cemetery_app" }.ConnectionString;
        await DatabaseInitializer.ApplyAsync(owner, app, cancellationToken);

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Owner", owner);
            builder.UseSetting("ConnectionStrings:App", app);
            builder.UseSetting("App:PublicUrl", "http://localhost:5173");
        });

        var alpha = factory.CreateClient();
        var beta = factory.CreateClient();
        await RegisterAsync(alpha, "alpha@example.com", cancellationToken);
        await RegisterAsync(beta, "beta@example.com", cancellationToken);
        var alphaOrg = await CreateOrganizationAsync(alpha, "Alpha Cemetery", cancellationToken);
        var betaOrg = await CreateOrganizationAsync(beta, "Beta Cemetery", cancellationToken);

        var alphaList = await alpha.GetFromJsonAsync<JsonElement>("/api/v1/organizations", cancellationToken);
        Assert.DoesNotContain(betaOrg, ReadIds(alphaList));

        var forbidden = await alpha.PostAsJsonAsync("/api/v1/organizations/switch", new { organizationId = betaOrg }, cancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var problem = await forbidden.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal("tenant.forbidden", problem.GetProperty("errorCode").GetString());

        var dashboard = await alpha.GetFromJsonAsync<JsonElement>("/api/v1/dashboard", cancellationToken);
        Assert.Equal(alphaOrg, dashboard.GetProperty("organizationId").GetGuid());
        Assert.Equal(0, dashboard.GetProperty("graveSiteCount").GetInt32());
    }

    private static async Task RegisterAsync(HttpClient client, string email, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = "Correct-Horse-1",
            displayName = "Clerk",
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateOrganizationAsync(HttpClient client, string name, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/organizations", new { name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private static IEnumerable<Guid> ReadIds(JsonElement organizations) =>
        organizations.EnumerateArray().Select(item => item.GetProperty("id").GetGuid());
}

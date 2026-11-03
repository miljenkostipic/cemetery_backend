using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cemetery.Domain.Identity;
using Npgsql;

namespace Cemetery.Api.IntegrationTests;

public sealed class WalkingSkeletonTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task A_user_cannot_read_another_organizations_dashboard()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.Equal(1, ApiFixture.ContainersStarted);
        var alpha = await Api.SignedInClientAsync(MembershipRole.OrganizationAdmin, "Alpha", cancellationToken);
        var beta = await Api.SignedInClientAsync(MembershipRole.OrganizationAdmin, "Beta", cancellationToken);
        await AssertIsolatedAsync(alpha, beta, cancellationToken);
        await AssertHistoryAsync(Api.OwnerConnection, cancellationToken);
    }

    [Fact]
    public async Task Seed_helpers_create_a_cemetery_site_and_deceased()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = await Api.SignedInClientAsync(MembershipRole.CemeteryClerk, "A", cancellationToken);
        var cemeteryId = await Api.SeedCemeteryAsync(client, cancellationToken);
        var siteId = await Api.SeedGraveSiteAsync(client, cemeteryId, cancellationToken);
        var deceasedId = await Api.SeedDeceasedAsync(client, cemeteryId, cancellationToken);
        await AssertStoredAsync(client, cemeteryId, siteId, deceasedId, cancellationToken);
    }

    private static async Task AssertIsolatedAsync(HttpClient alpha, HttpClient beta, CancellationToken cancellationToken)
    {
        var alphaOrg = await OrganizationIdAsync(alpha, cancellationToken);
        var betaOrg = await OrganizationIdAsync(beta, cancellationToken);
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

    private static async Task AssertStoredAsync(
        HttpClient client,
        Guid cemeteryId,
        Guid siteId,
        Guid deceasedId,
        CancellationToken cancellationToken)
    {
        Assert.NotEqual(Guid.Empty, cemeteryId);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/grave-sites/{siteId}", cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/deceased/{deceasedId}", cancellationToken)).StatusCode);
    }

    private static async Task<Guid> OrganizationIdAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/organizations", cancellationToken);
        return list.EnumerateArray().Single().GetProperty("id").GetGuid();
    }

    private static async Task AssertHistoryAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(HistorySql(), connection);
        var aligned = (bool)(await command.ExecuteScalarAsync(cancellationToken))!;
        Assert.True(aligned);
    }

    private static string HistorySql() => """
        SELECT to_regclass('cemetery.__ef_migrations') IS NOT NULL
           AND to_regclass('public."__EFMigrationsHistory"') IS NULL
        """;

    private static IEnumerable<Guid> ReadIds(JsonElement organizations) =>
        organizations.EnumerateArray().Select(item => item.GetProperty("id").GetGuid());
}

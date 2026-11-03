using System.Net.Http.Json;
using Cemetery.Domain.Identity;

namespace Cemetery.Api.IntegrationTests;

public sealed partial class ApiFixture
{
    private const string Password = "Correct-Horse-1";

    public Task<HttpClient> SignedInClientAsync(
        MembershipRole role,
        string tenant = "A",
        CancellationToken cancellationToken = default) =>
        role == MembershipRole.OrganizationAdmin && !admins.ContainsKey(tenant)
            ? OpenTenantAsync(tenant, cancellationToken)
            : JoinTenantAsync(role, tenant, cancellationToken);

    private async Task<HttpClient> OpenTenantAsync(string tenant, CancellationToken cancellationToken)
    {
        var (client, _) = await RegisterAsync(tenant, MembershipRole.OrganizationAdmin, cancellationToken);
        await CreateOrganizationAsync(client, tenant, cancellationToken);
        admins.Add(tenant, client);
        return client;
    }

    private async Task<HttpClient> JoinTenantAsync(MembershipRole role, string tenant, CancellationToken cancellationToken)
    {
        var admin = await AdminAsync(tenant, cancellationToken);
        var (client, email) = await RegisterAsync(tenant, role, cancellationToken);
        await InviteAsync(admin, email, role, cancellationToken);
        await AcceptAsync(client, email, cancellationToken);
        return client;
    }

    private Task<HttpClient> AdminAsync(string tenant, CancellationToken cancellationToken) =>
        admins.TryGetValue(tenant, out var admin) ? Task.FromResult(admin) : OpenTenantAsync(tenant, cancellationToken);

    private async Task<(HttpClient Client, string Email)> RegisterAsync(
        string tenant,
        MembershipRole role,
        CancellationToken cancellationToken)
    {
        var email = NextEmail(tenant, role);
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            password = Password,
            displayName = "Clerk",
        }, cancellationToken);
        await EnsureOkAsync(response);
        return (client, email);
    }

    private static async Task CreateOrganizationAsync(HttpClient client, string tenant, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/organizations", new { name = OrganizationName(tenant) }, cancellationToken);
        await EnsureOkAsync(response);
    }

    private static async Task InviteAsync(HttpClient admin, string email, MembershipRole role, CancellationToken cancellationToken)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/organizations/invitations", new
        {
            email,
            role = MembershipRoles.ToCode(role),
        }, cancellationToken);
        await EnsureOkAsync(response);
    }

    private async Task AcceptAsync(HttpClient client, string email, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync("/api/v1/invitations/accept", new { token = inbox.TakeInvitation(email) }, cancellationToken);
        await EnsureOkAsync(response);
    }

    private string NextEmail(string tenant, MembershipRole role)
    {
        var label = new string(tenant.Where(char.IsAsciiLetterOrDigit).ToArray());
        if (label.Length == 0)
            label = "tenant";

        userCount++;
        return $"{label}.{MembershipRoles.ToCode(role)}.{userCount}@example.com";
    }

    private static string OrganizationName(string tenant)
    {
        var name = tenant.Trim();
        return name.Length >= 2 ? name : $"Tenant {name}";
    }
}

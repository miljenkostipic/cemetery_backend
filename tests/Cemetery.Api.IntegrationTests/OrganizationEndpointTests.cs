using System.Net;
using System.Net.Http.Json;
using Cemetery.Domain.Identity;

namespace Cemetery.Api.IntegrationTests;

public sealed class OrganizationEndpointTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Happy_path()
    {
        var client = Api.CreateClient();
        var email = $"ada.{Guid.NewGuid():N}@example.com";
        await OfficeWorld.Post(client, "/api/v1/auth/register", new { email, password = "Correct-Horse-1", displayName = "Ada" }, CancellationToken);
        var created = await OfficeWorld.Post(client, "/api/v1/organizations", new { name = "Alpha Grounds" }, CancellationToken);
        var organizationId = ApiAssert.Id(created);
        var listed = await OfficeWorld.Get(client, "/api/v1/organizations", CancellationToken);
        Assert.Contains(organizationId, ApiAssert.Ids(listed));

        var holderEmail = $"holder.{Guid.NewGuid():N}@example.com";
        await OfficeWorld.Post(client, "/api/v1/organizations/invitations", new { email = holderEmail, role = "grave_holder" }, CancellationToken);
        var token = Api.TakeInvitation(holderEmail);
        var holder = Api.CreateClient();
        await OfficeWorld.Post(holder, "/api/v1/auth/register", new { email = holderEmail, password = "Correct-Horse-1", displayName = "Iva" }, CancellationToken);
        var accepted = await OfficeWorld.Post(holder, "/api/v1/invitations/accept", new { token }, CancellationToken);
        Assert.Equal("grave_holder", accepted.GetProperty("role").GetString());

        var dashboard = await OfficeWorld.Get(client, "/api/v1/dashboard", CancellationToken);
        Assert.Equal("Alpha Grounds", dashboard.GetProperty("name").GetString());

        var second = await OfficeWorld.Post(client, "/api/v1/organizations", new { name = "Beta Grounds" }, CancellationToken);
        var switched = await OfficeWorld.Post(client, "/api/v1/organizations/switch", new { organizationId = ApiAssert.Id(second) }, CancellationToken);
        Assert.Equal(ApiAssert.Id(second), switched.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Wrong_role_is_403()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var invite = await world.Field.PostAsJsonAsync("/api/v1/organizations/invitations", new { email = "x@example.com", role = "cemetery_clerk" }, CancellationToken);
        await ApiAssert.Problem(invite, HttpStatusCode.Forbidden, "access.forbidden");

        var solo = Api.CreateClient();
        await OfficeWorld.Post(solo, "/api/v1/auth/register", new { email = $"solo.{Guid.NewGuid():N}@example.com", password = "Correct-Horse-1", displayName = "Solo" }, CancellationToken);
        var dashboard = await solo.GetAsync("/api/v1/dashboard", CancellationToken);
        await ApiAssert.Problem(dashboard, HttpStatusCode.Forbidden, "access.forbidden");
    }

    [Fact]
    public async Task Other_tenant_is_404()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var mine = await OfficeWorld.Get(world.Clerk, "/api/v1/organizations", CancellationToken);
        var theirs = await OfficeWorld.Get(world.Other, "/api/v1/organizations", CancellationToken);
        Assert.Empty(ApiAssert.Ids(mine).Intersect(ApiAssert.Ids(theirs)));
        var foreign = ApiAssert.FirstId(mine);
        var switched = await world.Other.PostAsJsonAsync("/api/v1/organizations/switch", new { organizationId = foreign }, CancellationToken);
        await ApiAssert.Problem(switched, HttpStatusCode.Forbidden, "tenant.forbidden");
    }

    [Theory]
    [InlineData("/api/v1/organizations", "organization.name_invalid")]
    [InlineData("/api/v1/organizations/invitations", "auth.email_invalid")]
    [InlineData("/api/v1/organizations/switch", "organization.invalid")]
    [InlineData("/api/v1/invitations/accept", "invitation.invalid")]
    public async Task Validation_is_400(string url, string code)
    {
        var admin = await Api.SignedInClientAsync(MembershipRole.OrganizationAdmin, Guid.NewGuid().ToString("N")[..8], CancellationToken);
        var response = await admin.PostAsJsonAsync(url, new { name = " ", email = "bad", role = "grave_holder", organizationId = Guid.Empty, token = "" }, CancellationToken);
        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, code);
    }
}

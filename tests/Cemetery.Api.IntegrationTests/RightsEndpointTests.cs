using System.Net;
using System.Net.Http.Json;

namespace Cemetery.Api.IntegrationTests;

public sealed class RightsEndpointTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Happy_path()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var sent = await OfficeWorld.Post(world.Holder, "/api/v1/me/requests", new { message = "Please check the headstone", graveSiteId = world.SiteId }, CancellationToken);
        var id = ApiAssert.Id(sent);
        Assert.Contains(id, ApiAssert.Ids(await OfficeWorld.Get(world.Holder, "/api/v1/me/requests", CancellationToken)));
        Assert.Contains(id, ApiAssert.Ids(await OfficeWorld.Get(world.Clerk, "/api/v1/office-requests", CancellationToken)));
    }

    [Fact]
    public async Task Wrong_role_is_403()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Clerk.GetAsync("/api/v1/me/requests", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync("/api/v1/me/requests", new { message = "Please check the headstone", graveSiteId = world.SiteId }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync("/api/v1/office-requests", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
    }

    [Fact]
    public async Task Other_tenant_is_404()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var sent = await OfficeWorld.Post(world.Holder, "/api/v1/me/requests", new { message = "Please check the headstone", graveSiteId = world.SiteId }, CancellationToken);
        var office = await OfficeWorld.Get(world.Other, "/api/v1/office-requests", CancellationToken);
        Assert.DoesNotContain(ApiAssert.Id(sent), ApiAssert.Ids(office));
    }

    [Fact]
    public async Task Validation_is_400()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync("/api/v1/me/requests", new { message = " ", graveSiteId = world.SiteId }, CancellationToken), HttpStatusCode.BadRequest, "request.message_invalid");
    }
}

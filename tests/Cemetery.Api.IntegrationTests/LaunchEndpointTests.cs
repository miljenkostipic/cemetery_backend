using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cemetery.Api.IntegrationTests;

public sealed class LaunchEndpointTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Happy_path()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var interment = await OfficeWorld.PostId(world.Clerk, "/api/v1/interments", new
        {
            deceasedId = world.DeceasedId,
            graveSiteId = world.SiteId,
            position = 1,
            kind = "coffin",
            buriedOn = "2001-03-05",
        }, CancellationToken);
        Assert.Contains(interment, ApiAssert.Ids(await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/interments", CancellationToken)));
        var link = await OfficeWorld.PostId(world.Clerk, $"/api/v1/interments/{interment}/matica-links", new { deathCertificateId = Guid.CreateVersion7() }, CancellationToken);
        Assert.Contains(link, ApiAssert.Ids(await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/matica-links", CancellationToken)));

        var created = await OfficeWorld.Post(world.Clerk, "/api/v1/integrations/webhooks", new { url = "https://hooks.example.com/cemetery", @event = "interment.recorded" }, CancellationToken);
        var webhookId = ApiAssert.Id(created);
        Assert.Contains(webhookId, ApiAssert.Ids(await OfficeWorld.Get(world.Clerk, "/api/v1/integrations/webhooks", CancellationToken)));
        await ApiAssert.Ok(await world.Clerk.DeleteAsync($"/api/v1/integrations/webhooks/{webhookId}", CancellationToken));

        await OfficeWorld.Get(world.Clerk, "/api/v1/gdpr/records", CancellationToken);
        var export = await OfficeWorld.Post(world.Clerk, "/api/v1/gdpr/exports", new { }, CancellationToken);
        Assert.Equal(JsonValueKind.Object, export.ValueKind);

        var report = await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/reports", CancellationToken);
        Assert.Equal(JsonValueKind.Object, report.ValueKind);
    }

    [Fact]
    public async Task Wrong_role_is_403()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/interments", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/matica-links", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/interments/{Guid.NewGuid()}/matica-links", new { deathCertificateId = Guid.CreateVersion7() }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync("/api/v1/integrations/webhooks", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync("/api/v1/integrations/webhooks", new { url = "https://hooks.example.com/cemetery", @event = "interment.recorded" }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.DeleteAsync($"/api/v1/integrations/webhooks/{Guid.NewGuid()}", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync("/api/v1/gdpr/records", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsync("/api/v1/gdpr/exports", null, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/reports", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
    }

    [Fact]
    public async Task Other_tenant_is_404()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var interment = await OfficeWorld.PostId(world.Clerk, "/api/v1/interments", new
        {
            deceasedId = world.DeceasedId,
            graveSiteId = world.SiteId,
            position = 1,
            kind = "coffin",
            buriedOn = "2001-03-05",
        }, CancellationToken);
        await OfficeWorld.Hidden(world.Other, $"/api/v1/cemeteries/{world.CemeteryId}/interments", interment, CancellationToken);
        await OfficeWorld.Hidden(world.Other, $"/api/v1/cemeteries/{world.CemeteryId}/matica-links", null, CancellationToken);
        var report = await world.Other.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/reports", CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, report.StatusCode);
    }

    [Fact]
    public async Task Validation_is_400()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/interments/{Guid.Empty}/matica-links", new { deathCertificateId = Guid.CreateVersion7() }, CancellationToken), HttpStatusCode.BadRequest, "matica.interment_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync("/api/v1/integrations/webhooks", new { url = "not-a-url", @event = "interment.recorded" }, CancellationToken), HttpStatusCode.BadRequest, "webhook.url_invalid");
        await ApiAssert.Problem(await world.Clerk.DeleteAsync($"/api/v1/integrations/webhooks/{Guid.Empty}", CancellationToken), HttpStatusCode.BadRequest, "webhook.not_found");
    }
}

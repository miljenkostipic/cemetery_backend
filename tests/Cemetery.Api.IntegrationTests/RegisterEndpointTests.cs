using System.Net;
using System.Net.Http.Json;

namespace Cemetery.Api.IntegrationTests;

public sealed class RegisterEndpointTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Happy_path()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var people = await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/deceased", CancellationToken);
        Assert.Contains(world.DeceasedId, ApiAssert.Ids(people));
        var person = await OfficeWorld.Get(world.Clerk, $"/api/v1/deceased/{world.DeceasedId}", CancellationToken);
        Assert.Equal("Ana", person.GetProperty("givenName").GetString());

        var corrected = await OfficeWorld.Post(world.Clerk, $"/api/v1/deceased/{world.DeceasedId}/corrections", new
        {
            givenName = "Anna",
            familyName = "Kovac",
            bornOn = "1920-01-02",
            diedOn = "2001-03-04",
            reason = "Spelling",
        }, CancellationToken);
        Assert.Equal("Anna", corrected.GetProperty("givenName").GetString());

        var period = await world.Clerk.PutAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/rest-period", new { years = 20 }, CancellationToken);
        await ApiAssert.Ok(period);
        var positions = await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/positions", CancellationToken);
        Assert.True(positions.GetArrayLength() > 0);

        var interment = await OfficeWorld.Post(world.Clerk, "/api/v1/interments", new
        {
            deceasedId = world.DeceasedId,
            graveSiteId = world.SiteId,
            position = 1,
            kind = "coffin",
            buriedOn = "2001-03-05",
        }, CancellationToken);
        var intermentId = ApiAssert.Id(interment);
        var onSite = await OfficeWorld.Get(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/interments", CancellationToken);
        Assert.Contains(intermentId, ApiAssert.Ids(onSite));

        var moved = await OfficeWorld.Post(world.Clerk, $"/api/v1/interments/{intermentId}/transfers", new
        {
            graveSiteId = world.SecondSiteId,
            position = 1,
            on = "2010-04-01",
        }, CancellationToken);
        Assert.Equal(world.SecondSiteId, moved.GetProperty("graveSiteId").GetGuid());
        // A transfer closes the original interment and opens a new one at the destination.
        var movedId = ApiAssert.Id(moved);
        var exhumed = await OfficeWorld.Post(world.Clerk, $"/api/v1/interments/{movedId}/exhumations", new { on = "2011-05-01" }, CancellationToken);
        Assert.Equal(movedId, ApiAssert.Id(exhumed));

        var extra = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/deceased", new
        {
            givenName = "Ivo",
            familyName = "Horvat",
            bornOn = "1930-01-01",
            diedOn = "1999-01-01",
        }, CancellationToken);
        var removed = await world.Clerk.DeleteAsync($"/api/v1/deceased/{extra}", CancellationToken);
        await ApiAssert.Ok(removed);
    }

    [Fact]
    public async Task Wrong_role_is_403()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/deceased", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/deceased/{world.DeceasedId}", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/positions", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/grave-sites/{world.SiteId}/interments", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/deceased", new { givenName = "A", familyName = "B" }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PutAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/rest-period", new { years = 10 }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync("/api/v1/interments", new { deceasedId = world.DeceasedId, graveSiteId = world.SiteId, position = 1, kind = "coffin", buriedOn = "2001-03-05" }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.DeleteAsync($"/api/v1/deceased/{world.DeceasedId}", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
    }

    [Fact]
    public async Task Other_tenant_is_404()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Other.GetAsync($"/api/v1/deceased/{world.DeceasedId}", CancellationToken)).StatusCode);
        await OfficeWorld.Hidden(world.Other, $"/api/v1/cemeteries/{world.CemeteryId}/deceased", world.DeceasedId, CancellationToken);
        await OfficeWorld.Hidden(world.Other, $"/api/v1/cemeteries/{world.CemeteryId}/positions", null, CancellationToken);
        await OfficeWorld.Hidden(world.Other, $"/api/v1/grave-sites/{world.SiteId}/interments", null, CancellationToken);
    }

    [Fact]
    public async Task Validation_is_400()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/deceased", new { givenName = "", familyName = "Kovac" }, CancellationToken), HttpStatusCode.BadRequest, "deceased.given_name_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/deceased/{world.DeceasedId}/corrections", new { givenName = "Ana", familyName = "Kovac", reason = "" }, CancellationToken), HttpStatusCode.BadRequest, "deceased.reason_required");
        await ApiAssert.Problem(await world.Clerk.PutAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/rest-period", new { years = 0 }, CancellationToken), HttpStatusCode.BadRequest, "cemetery.rest_period_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync("/api/v1/interments", new { deceasedId = world.DeceasedId, graveSiteId = world.SiteId, position = 1, kind = "nope", buriedOn = "2001-03-05" }, CancellationToken), HttpStatusCode.BadRequest, "interment.kind_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/interments/{Guid.Empty}/exhumations", new { on = "2011-05-01" }, CancellationToken), HttpStatusCode.BadRequest, "interment.not_found");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/interments/{Guid.Empty}/transfers", new { graveSiteId = world.SiteId, position = 0, on = "2010-04-01" }, CancellationToken), HttpStatusCode.BadRequest, "interment.position_invalid");
        await ApiAssert.Problem(await world.Clerk.DeleteAsync($"/api/v1/deceased/{Guid.Empty}", CancellationToken), HttpStatusCode.BadRequest, "deceased.not_found");
    }
}

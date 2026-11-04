using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cemetery.Api.IntegrationTests;

public sealed class PublicEndpointTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Unpublished_register_returns_nothing()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var slug = await Slug(world);
        var listed = await OfficeWorld.Get(Api.CreateClient(), "/api/v1/public/cemeteries", CancellationToken);
        Assert.DoesNotContain(world.CemeteryId, listed.EnumerateArray().Select(item => item.GetProperty("cemeteryId").GetGuid()));
        Assert.Equal(HttpStatusCode.NotFound, (await Api.CreateClient().GetAsync($"/api/v1/public/cemeteries/{slug}/{world.CemeteryId}", CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Hide_recent_deaths_hides_a_death_today()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var recent = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/deceased", new
        {
            givenName = "Marko",
            familyName = "Novak",
            bornOn = "1950-01-01",
            diedOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        }, CancellationToken);
        var slug = await Slug(world);
        await Put(world, new { hideRecentDeathsDays = 30 });

        var oldHits = await Search(slug, "Kovac");
        Assert.Contains(world.DeceasedId, oldHits);
        var hidden = await Search(slug, "Novak");
        Assert.DoesNotContain(recent, hidden);

        await Put(world, new { hideRecentDeathsDays = 0 });
        Assert.Contains(recent, await Search(slug, "Novak"));
    }

    [Fact]
    public async Task Hidden_sites_never_appear()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await OfficeWorld.Post(world.Clerk, "/api/v1/interments", new
        {
            deceasedId = world.DeceasedId,
            graveSiteId = world.SiteId,
            position = 1,
            kind = "coffin",
            buriedOn = "2001-03-05",
        }, CancellationToken);
        var slug = await Slug(world);
        await Put(world, new { hideRecentDeathsDays = 0 });
        var shown = await Map(slug, world.CemeteryId);
        Assert.Contains(world.SiteId, SiteIds(shown));

        await OfficeWorld.Post(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/hide-from-public", new { }, CancellationToken);
        Assert.DoesNotContain(world.SiteId, SiteIds(await Map(slug, world.CemeteryId)));
        await OfficeWorld.Post(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/show-on-public-map", new { }, CancellationToken);
        Assert.Contains(world.SiteId, SiteIds(await Map(slug, world.CemeteryId)));

        var memorial = await world.Clerk.PutAsJsonAsync($"/api/v1/deceased/{world.DeceasedId}/memorial", new { epitaph = "Remembered", photoUrl = (string?)null }, CancellationToken);
        await ApiAssert.Ok(memorial);
        var page = await OfficeWorld.Get(Api.CreateClient(), $"/api/v1/public/memorials/{world.DeceasedId}", CancellationToken);
        Assert.Equal(world.DeceasedId, page.GetProperty("deceasedId").GetGuid());

        var read = await OfficeWorld.Get(world.Clerk, "/api/v1/organizations/visibility", CancellationToken);
        Assert.True(read.GetProperty("publishesRegister").GetBoolean());
        var withdrawn = await world.Clerk.DeleteAsync("/api/v1/organizations/visibility", CancellationToken);
        await ApiAssert.Ok(withdrawn);
        Assert.DoesNotContain(world.CemeteryId, (await OfficeWorld.Get(Api.CreateClient(), "/api/v1/public/cemeteries", CancellationToken)).EnumerateArray().Select(item => item.GetProperty("cemeteryId").GetGuid()));
    }

    [Fact]
    public async Task Wrong_role_is_403()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Holder.GetAsync("/api/v1/organizations/visibility", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PutAsJsonAsync("/api/v1/organizations/visibility", new { hideRecentDeathsDays = 0 }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.DeleteAsync("/api/v1/organizations/visibility", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsync($"/api/v1/grave-sites/{world.SiteId}/hide-from-public", null, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsync($"/api/v1/grave-sites/{world.SiteId}/show-on-public-map", null, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PutAsJsonAsync($"/api/v1/deceased/{world.DeceasedId}/memorial", new { epitaph = "No" }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
    }

    [Fact]
    public async Task Validation_is_400()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Clerk.PutAsJsonAsync("/api/v1/organizations/visibility", new { hideRecentDeathsDays = 4000 }, CancellationToken), HttpStatusCode.BadRequest, "visibility.days_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsync($"/api/v1/grave-sites/{Guid.Empty}/hide-from-public", null, CancellationToken), HttpStatusCode.BadRequest, "grave_site.not_found");
        await ApiAssert.Problem(await world.Clerk.PostAsync($"/api/v1/grave-sites/{Guid.Empty}/show-on-public-map", null, CancellationToken), HttpStatusCode.BadRequest, "grave_site.not_found");
        await ApiAssert.Problem(await world.Clerk.PutAsJsonAsync($"/api/v1/deceased/{Guid.Empty}/memorial", new { epitaph = "No" }, CancellationToken), HttpStatusCode.BadRequest, "deceased.not_found");
    }

    private static async Task Put(OfficeWorld world, object body) =>
        await ApiAssert.Ok(await world.Clerk.PutAsJsonAsync("/api/v1/organizations/visibility", body, CancellationToken));

    private static async Task<string> Slug(OfficeWorld world)
    {
        var dashboard = await OfficeWorld.Get(world.Clerk, "/api/v1/dashboard", CancellationToken);
        return dashboard.GetProperty("slug").GetString() ?? "";
    }

    private async Task<IEnumerable<Guid>> Search(string slug, string name)
    {
        var all = await OfficeWorld.Get(Api.CreateClient(), $"/api/v1/public/search?name={name}", CancellationToken);
        var one = await OfficeWorld.Get(Api.CreateClient(), $"/api/v1/public/cemeteries/{slug}/search?name={name}", CancellationToken);
        return Ids(all).Concat(Ids(one));
    }

    private async Task<JsonElement> Map(string slug, Guid cemeteryId) =>
        await OfficeWorld.Get(Api.CreateClient(), $"/api/v1/public/cemeteries/{slug}/{cemeteryId}", CancellationToken);

    private static IEnumerable<Guid> Ids(JsonElement list) =>
        list.EnumerateArray().Select(item => item.GetProperty("deceasedId").GetGuid());

    private static IEnumerable<Guid> SiteIds(JsonElement map) =>
        map.GetProperty("sites").EnumerateArray().Select(item => item.GetProperty("id").GetGuid());
}

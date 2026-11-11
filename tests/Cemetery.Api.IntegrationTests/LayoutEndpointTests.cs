using System.Net;
using System.Net.Http.Json;

namespace Cemetery.Api.IntegrationTests;

public sealed class LayoutEndpointTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Happy_path()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var listed = await OfficeWorld.Get(world.Clerk, "/api/v1/cemeteries", CancellationToken);
        Assert.Contains(world.CemeteryId, ApiAssert.Ids(listed));
        var cemetery = await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}", CancellationToken);
        Assert.Equal(world.CemeteryId, ApiAssert.Id(cemetery));

        await OfficeWorld.Post(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/plan", new
        {
            planImageUrl = "https://maps.example.com/plan.png",
            planBounds = OfficeWorld.Ring(),
        }, CancellationToken);
        var sections = await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/sections", CancellationToken);
        Assert.Contains(world.SectionId, ApiAssert.Ids(sections));
        var sites = await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/grave-sites", CancellationToken);
        Assert.Contains(world.SiteId, ApiAssert.Ids(sites));

        var spare = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/sections", new { name = "Spare", code = "S2" }, CancellationToken);
        var outlined = await world.Clerk.PutAsJsonAsync($"/api/v1/sections/{spare}/outline", new { outline = OfficeWorld.Ring() }, CancellationToken);
        await ApiAssert.Ok(outlined);
        var row = await OfficeWorld.PostId(world.Clerk, $"/api/v1/sections/{spare}/rows", new { label = "1" }, CancellationToken);
        var rows = await OfficeWorld.Get(world.Clerk, $"/api/v1/sections/{spare}/rows", CancellationToken);
        Assert.Contains(row, ApiAssert.Ids(rows));
        var generated = await OfficeWorld.Post(world.Clerk, $"/api/v1/sections/{spare}/grave-sites", new
        {
            kind = "single",
            rows = 1,
            columns = 2,
            originLongitude = 16.2,
            originLatitude = 45.9,
            widthMeters = 1.0,
            depthMeters = 2.0,
            gapMeters = 0.3,
            rotationDegrees = 0.0,
        }, CancellationToken);
        Assert.Equal(2, generated.GetArrayLength());

        var empty = await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/sections", new { name = "Empty", code = "S3" }, CancellationToken);
        var removed = await world.Clerk.DeleteAsync($"/api/v1/sections/{empty}", CancellationToken);
        await ApiAssert.Ok(removed);

        var site = await OfficeWorld.Get(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}", CancellationToken);
        Assert.Equal(world.SiteId, ApiAssert.Id(site));
        var moved = new object[]
        {
            new { longitude = 15.9799, latitude = 45.81 },
            new { longitude = 15.97991, latitude = 45.81 },
            new { longitude = 15.97991, latitude = 45.81002 },
            new { longitude = 15.9799, latitude = 45.81002 },
            new { longitude = 15.9799, latitude = 45.81 },
        };
        var redrawn = await world.Clerk.PutAsJsonAsync($"/api/v1/grave-sites/{world.SiteId}/outline", new { outline = moved }, CancellationToken);
        await ApiAssert.Ok(redrawn);

        var halves = await OfficeWorld.Post(world.Clerk, $"/api/v1/grave-sites/{world.SecondSiteId}/split", new { }, CancellationToken);
        var left = ApiAssert.FirstId(halves);
        var right = halves.EnumerateArray().ElementAt(1).GetProperty("id").GetGuid();
        var restored = await OfficeWorld.Post(world.Clerk, "/api/v1/grave-sites/undo-split", new { originalId = world.SecondSiteId, leftId = left, rightId = right }, CancellationToken);
        Assert.Equal(world.SecondSiteId, ApiAssert.Id(restored));

        var pair = generated.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();
        var merged = await OfficeWorld.Post(world.Clerk, "/api/v1/grave-sites/merge", new { leftId = pair[0], rightId = pair[1] }, CancellationToken);
        Assert.NotEqual(Guid.Empty, ApiAssert.Id(merged));

        var closed = await OfficeWorld.Post(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/close", new { }, CancellationToken);
        Assert.Equal(world.SiteId, ApiAssert.Id(closed));
        var reopened = await OfficeWorld.Post(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/reopen", new { }, CancellationToken);
        Assert.Equal(world.SiteId, ApiAssert.Id(reopened));
    }

    [Fact]
    public async Task Wrong_role_is_403()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        string[] urls =
        [
            "/api/v1/cemeteries",
            $"/api/v1/cemeteries/{world.CemeteryId}",
            $"/api/v1/cemeteries/{world.CemeteryId}/sections",
            $"/api/v1/cemeteries/{world.CemeteryId}/grave-sites",
            $"/api/v1/sections/{world.SectionId}/rows",
            $"/api/v1/grave-sites/{world.SiteId}",
        ];
        foreach (var url in urls)
            await ApiAssert.Problem(await world.Holder.GetAsync(url, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");

        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync("/api/v1/cemeteries", new { name = "Nope", schematic = true }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/plan", new { planImageUrl = "https://maps.example.com/plan.png", planBounds = OfficeWorld.Ring() }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/sections", new { name = "Nope", code = "X" }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.DeleteAsync($"/api/v1/sections/{world.SectionId}", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PutAsJsonAsync($"/api/v1/sections/{world.SectionId}/outline", new { outline = OfficeWorld.Ring() }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/sections/{world.SectionId}/rows", new { label = "9" }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/sections/{world.SectionId}/grave-sites", OfficeWorld.Grid(1), CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync("/api/v1/grave-sites/merge", new { leftId = world.SiteId, rightId = world.SecondSiteId }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync("/api/v1/grave-sites/undo-split", new { originalId = world.SiteId, leftId = world.SiteId, rightId = world.SecondSiteId }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PutAsJsonAsync($"/api/v1/grave-sites/{world.SiteId}/outline", new { outline = OfficeWorld.Ring() }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsync($"/api/v1/grave-sites/{world.SiteId}/split", null, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsync($"/api/v1/grave-sites/{world.SiteId}/close", null, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsync($"/api/v1/grave-sites/{world.SiteId}/reopen", null, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
    }

    [Fact]
    public async Task Other_tenant_is_404()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Other.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Other.GetAsync($"/api/v1/grave-sites/{world.SiteId}", CancellationToken)).StatusCode);
        var cemeteries = await OfficeWorld.Get(world.Other, "/api/v1/cemeteries", CancellationToken);
        Assert.DoesNotContain(world.CemeteryId, ApiAssert.Ids(cemeteries));
        var sections = await OfficeWorld.Get(world.Other, $"/api/v1/cemeteries/{world.CemeteryId}/sections", CancellationToken);
        Assert.DoesNotContain(world.SectionId, ApiAssert.Ids(sections));
        var sites = await OfficeWorld.Get(world.Other, $"/api/v1/cemeteries/{world.CemeteryId}/grave-sites", CancellationToken);
        Assert.DoesNotContain(world.SiteId, ApiAssert.Ids(sites));
        var rows = await OfficeWorld.Get(world.Other, $"/api/v1/sections/{world.SectionId}/rows", CancellationToken);
        Assert.Empty(rows.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await world.Other.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/plan", new { planImageUrl = "https://maps.example.com/plan.png", planBounds = OfficeWorld.Ring() }, CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Other.PostAsync($"/api/v1/grave-sites/{world.SiteId}/split", null, CancellationToken)).StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/cemeteries", "cemetery.name_invalid")]
    public async Task Validation_is_400(string url, string code)
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync(url, new { name = " ", schematic = true }, CancellationToken), HttpStatusCode.BadRequest, code);
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/plan", new { planImageUrl = "", planBounds = OfficeWorld.Ring() }, CancellationToken), HttpStatusCode.BadRequest, "cemetery.plan_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/sections", new { name = " ", code = "S9" }, CancellationToken), HttpStatusCode.BadRequest, "section.name_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/sections/{world.SectionId}/rows", new { label = "" }, CancellationToken), HttpStatusCode.BadRequest, "row.label_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync($"/api/v1/sections/{world.SectionId}/grave-sites", new { kind = "nope", rows = 1, columns = 1, originLongitude = 15.98, originLatitude = 45.81, widthMeters = 1.0, depthMeters = 2.0, gapMeters = 0.3, rotationDegrees = 0.0 }, CancellationToken), HttpStatusCode.BadRequest, "grave_site.kind_invalid");
        await ApiAssert.Problem(await world.Clerk.PutAsJsonAsync($"/api/v1/grave-sites/{world.SiteId}/outline", new { outline = Array.Empty<object>() }, CancellationToken), HttpStatusCode.BadRequest, "geometry.ring_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync("/api/v1/grave-sites/merge", new { leftId = world.SiteId, rightId = world.SiteId }, CancellationToken), HttpStatusCode.BadRequest, "grave_site.merge_invalid");
        await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync("/api/v1/grave-sites/undo-split", new { originalId = Guid.Empty, leftId = world.SiteId, rightId = world.SecondSiteId }, CancellationToken), HttpStatusCode.BadRequest, "grave_site.not_found");
        await ApiAssert.Problem(await world.Clerk.DeleteAsync($"/api/v1/sections/{Guid.Empty}", CancellationToken), HttpStatusCode.BadRequest, "section.not_found");
    }
}

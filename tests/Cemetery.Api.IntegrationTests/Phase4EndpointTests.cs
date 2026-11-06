using System.Net;
using System.Net.Http.Json;

namespace Cemetery.Api.IntegrationTests;

public sealed class Phase4EndpointTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Happy_path()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var holder = await OfficeWorld.Post(world.Clerk, "/api/v1/holders", Holder("Ana", "Kovačević"), CancellationToken);
        var holderId = ApiAssert.Id(holder);
        Assert.Contains(holderId, ApiAssert.Ids(await OfficeWorld.Get(world.Clerk, "/api/v1/holders?q=kovacevic", CancellationToken)));

        var right = await OfficeWorld.Post(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/right", new
        {
            perpetual = false,
            termYears = 10,
            holders = new[] { new { givenName = "Ana", familyName = "Kovačević", email = "ana@example.com", share = 100 } },
        }, CancellationToken);
        Assert.Equal(world.SiteId, right.GetProperty("graveSiteId").GetGuid());
        var rightId = ApiAssert.Id(right);
        var renewed = await OfficeWorld.Post(world.Clerk, $"/api/v1/grave-rights/{rightId}/renewals", new { years = 5 }, CancellationToken);
        Assert.Equal(rightId, ApiAssert.Id(renewed));

        var reserved = await OfficeWorld.Post(world.Clerk, $"/api/v1/grave-sites/{world.SecondSiteId}/reservations", new
        {
            givenName = "Ivo",
            familyName = "Horvat",
            email = "ivo@example.com",
            until = "2027-06-01",
        }, CancellationToken);
        var reservationId = ApiAssert.Id(reserved);
        var map = await world.Clerk.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/map", CancellationToken);
        Assert.Equal(HttpStatusCode.OK, map.StatusCode);
        Assert.False(string.IsNullOrEmpty(map.Headers.ETag?.Tag));
        using (var again = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cemeteries/{world.CemeteryId}/map"))
        {
            again.Headers.TryAddWithoutValidation("If-None-Match", map.Headers.ETag!.Tag);
            var cached = await world.Clerk.SendAsync(again, CancellationToken);
            Assert.Equal(HttpStatusCode.NotModified, cached.StatusCode);
        }

        await OfficeWorld.Post(world.Clerk, $"/api/v1/reservations/{reservationId}/cancellations", new { }, CancellationToken);
        var document = await OfficeWorld.Post(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/documents", new
        {
            fileName = "contract.png",
            contentType = "image/png",
            contentBase64 = "iVBORw0KGgo=",
        }, CancellationToken);
        var documentId = ApiAssert.Id(document);
        Assert.Contains(documentId, ApiAssert.Ids(await OfficeWorld.Get(world.Clerk, $"/api/v1/grave-sites/{world.SiteId}/documents", CancellationToken)));
        Assert.Equal(HttpStatusCode.OK, (await world.Clerk.GetAsync($"/api/v1/documents/{documentId}", CancellationToken)).StatusCode);

        var dossier = await OfficeWorld.Get(world.Field, $"/api/v1/grave-sites/{world.SiteId}/dossier", CancellationToken);
        Assert.Equal(world.SiteId, dossier.GetProperty("site").GetProperty("id").GetGuid());
        var hits = await OfficeWorld.Get(world.Clerk, $"/api/v1/search?q=kovacevic&cemeteryId={world.CemeteryId}", CancellationToken);
        Assert.Contains("holder", ApiAssert.Rows(hits).EnumerateArray().Select(item => item.GetProperty("kind").GetString()));
        var fieldHits = await OfficeWorld.Get(world.Field, $"/api/v1/search?q=kova&cemeteryId={world.CemeteryId}", CancellationToken);
        Assert.DoesNotContain("holder", ApiAssert.Rows(fieldHits).EnumerateArray().Select(item => item.GetProperty("kind").GetString()));

        var entrance = await OfficeWorld.Post(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/entrances", new { name = "North", longitude = 16.0, latitude = 45.8 }, CancellationToken);
        Assert.Equal("North", entrance.GetProperty("name").GetString());
        var path = await OfficeWorld.Post(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/paths", new
        {
            name = "Main",
            line = new[] { new { longitude = 16.0, latitude = 45.8 }, new { longitude = 16.001, latitude = 45.801 } },
        }, CancellationToken);
        Assert.Equal("Main", path.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Wrong_role_is_403()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await ApiAssert.Problem(await world.Holder.GetAsync("/api/v1/holders", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync($"/api/v1/grave-sites/{world.SiteId}/right", new { perpetual = true, holders = Array.Empty<object>() }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Clerk.GetAsync("/api/v1/me/grave-sites", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Clerk.GetAsync("/api/v1/me/charges", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/map", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Holder.GetAsync("/api/v1/search?q=ko", CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Field.PostAsJsonAsync($"/api/v1/cemeteries/{world.CemeteryId}/entrances", new { name = "North", longitude = 16.0, latitude = 45.8 }, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
    }

    [Fact]
    public async Task Other_tenant_is_404()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var holderId = await OfficeWorld.PostId(world.Clerk, "/api/v1/holders", Holder("Ana", "Horvat"), CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Other.GetAsync($"/api/v1/holders/{holderId}", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Other.GetAsync($"/api/v1/grave-sites/{world.SiteId}/right", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Other.GetAsync($"/api/v1/grave-sites/{world.SiteId}/dossier", CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Other.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/map", CancellationToken)).StatusCode);
        await OfficeWorld.Hidden(world.Other, "/api/v1/holders?q=horvat", holderId, CancellationToken);
    }

    private static object Holder(string given, string family) => new
    {
        givenName = given,
        familyName = family,
        oib = "12345678903",
        email = given.ToLowerInvariant() + "@example.com",
    };
}

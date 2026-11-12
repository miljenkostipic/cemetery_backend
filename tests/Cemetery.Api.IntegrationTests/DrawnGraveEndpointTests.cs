using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Cemetery.Api.IntegrationTests;

public sealed class DrawnGraveEndpointTests(ApiFixture api) : ApiTest(api)
{
    private const double MetersPerDegreeLatitude = 111_320;
    private const double Latitude = 45.0002;
    private const double Longitude = 16.0005;

    [Fact]
    public async Task Generated_graves_are_coded_field_row_place()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var first = await Codes(world.Clerk, world.CemeteryId);
        Assert.Contains("S1-01-01", first);
        Assert.Contains("S1-01-02", first);

        var generated = await OfficeWorld.Post(world.Clerk, $"/api/v1/sections/{world.SectionId}/grave-sites", new
        {
            kind = "single",
            rows = 2,
            columns = 2,
            originLongitude = 15.99,
            originLatitude = 45.81,
            widthMeters = 1.0,
            depthMeters = 2.0,
            gapMeters = 0.3,
            rotationDegrees = 30.0,
        }, CancellationToken);

        Assert.Equal(["S1-02-01", "S1-02-02", "S1-03-01", "S1-03-02"], generated.EnumerateArray().Select(Code));
    }

    [Fact]
    public async Task Generating_refuses_a_code_already_drawn()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await Draw(world.Clerk, world.SectionId, new { outline = Grave(0, 0), kind = "single", code = "S1-02-01" });

        var refused = await world.Clerk.PostAsJsonAsync($"/api/v1/sections/{world.SectionId}/grave-sites", new
        {
            kind = "single",
            rows = 1,
            columns = 1,
            originLongitude = 15.99,
            originLatitude = 45.81,
            widthMeters = 1.0,
            depthMeters = 2.0,
            gapMeters = 0.3,
            rotationDegrees = 0.0,
        }, CancellationToken);

        await ApiAssert.Problem(refused, HttpStatusCode.Conflict, "grave_site.code_taken");
    }

    [Fact]
    public async Task A_drawn_grave_takes_the_next_code_in_its_row_or_section()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var section = await OutlinedSection(world);
        var row = await OfficeWorld.PostId(world.Clerk, $"/api/v1/sections/{section}/rows", new { label = "3" }, CancellationToken);

        var loose = await Draw(world.Clerk, section, new { outline = Grave(0, 0), kind = "single" });
        var nextLoose = await Draw(world.Clerk, section, new { outline = Circle(5, 0, 1.2, 24), kind = "family", capacity = (int?)null, code = (string?)null, rowId = (Guid?)null });
        var placed = await Draw(world.Clerk, section, new { outline = Grave(10, 0), kind = "single", rowId = row });
        var nextPlaced = await Draw(world.Clerk, section, new { outline = LShape(15, 0), kind = "tomb", capacity = 4, rowId = row });
        var named = await Draw(world.Clerk, section, new { outline = Rotated(25, 0, 35), kind = "memorial", code = "  D-OLD-7 " });

        Assert.Equal("D-001", Code(loose));
        Assert.Equal(JsonValueKind.Null, loose.GetProperty("rowId").ValueKind);
        Assert.Equal(section, loose.GetProperty("sectionId").GetGuid());
        Assert.Equal(1, loose.GetProperty("capacity").GetInt32());
        Assert.Equal("available", loose.GetProperty("status").GetString());
        Assert.Equal(5, loose.GetProperty("outline").GetArrayLength());
        Assert.Equal("D-002", Code(nextLoose));
        Assert.Equal(2, nextLoose.GetProperty("capacity").GetInt32());
        Assert.Equal("D-03-01", Code(placed));
        Assert.Equal(row, placed.GetProperty("rowId").GetGuid());
        Assert.Equal("D-03-02", Code(nextPlaced));
        Assert.Equal(4, nextPlaced.GetProperty("capacity").GetInt32());
        Assert.Equal("D-OLD-7", Code(named));
        Assert.Equal("memorial", named.GetProperty("kind").GetString());
        Assert.Contains("D-03-02", await Codes(world.Clerk, world.CemeteryId));
    }

    [Fact]
    public async Task A_drawn_grave_is_refused_when_it_breaks_a_rule()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var section = await OutlinedSection(world);
        var otherRow = await OfficeWorld.PostId(world.Clerk, $"/api/v1/sections/{world.SectionId}/rows", new { label = "9" }, CancellationToken);
        await Draw(world.Clerk, section, new { outline = Grave(0, 0), kind = "single", code = "D-TAKEN" });
        var url = $"/api/v1/sections/{section}/grave-sites/drawn";

        async Task Refused(object body, HttpStatusCode status, string code) =>
            await ApiAssert.Problem(await world.Clerk.PostAsJsonAsync(url, body, CancellationToken), status, code);

        await Refused(new { outline = Grave(10, 0), kind = "single", code = "D-TAKEN" }, HttpStatusCode.Conflict, "grave_site.code_taken");
        await Refused(new { outline = Grave(0.5, 0.5), kind = "single" }, HttpStatusCode.UnprocessableEntity, "grave_site.overlaps");
        await Refused(new { outline = Grave(500, 0), kind = "single" }, HttpStatusCode.UnprocessableEntity, "grave_site.outside_section");
        await Refused(new { outline = Grave(10, 0, 0.5, 0.5), kind = "single" }, HttpStatusCode.UnprocessableEntity, "grave_site.size_invalid");
        await Refused(new { outline = Grave(10, 0, 15, 15), kind = "single" }, HttpStatusCode.UnprocessableEntity, "grave_site.size_invalid");
        await Refused(new { outline = Circle(10, 0, 1.5, 200), kind = "single" }, HttpStatusCode.BadRequest, "grave_site.outline_too_complex");
        await Refused(new { outline = Grave(10, 0), kind = "single", rowId = otherRow }, HttpStatusCode.NotFound, "grave_row.not_found");
        await Refused(new { outline = Grave(10, 0), kind = "castle" }, HttpStatusCode.BadRequest, "grave_site.kind_invalid");
        await Refused(new { outline = Grave(10, 0), kind = "single", code = new string('X', 33) }, HttpStatusCode.BadRequest, "grave_site.code_invalid");
        await Refused(new { outline = Grave(10, 0), kind = "memorial", capacity = 2 }, HttpStatusCode.UnprocessableEntity, "grave_site.capacity_invalid");
        await Refused(new { outline = Array.Empty<object>(), kind = "single" }, HttpStatusCode.BadRequest, "geometry.ring_invalid");
        await ApiAssert.Problem(
            await world.Clerk.PostAsJsonAsync($"/api/v1/sections/{Guid.CreateVersion7()}/grave-sites/drawn", new { outline = Grave(10, 0), kind = "single" }, CancellationToken),
            HttpStatusCode.NotFound,
            "section.not_found");
    }

    [Fact]
    public async Task Drawing_is_office_only_and_tenant_scoped()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var url = $"/api/v1/sections/{world.SectionId}/grave-sites/drawn";
        var body = new { outline = Grave(0, 0), kind = "single" };

        await ApiAssert.Problem(await world.Holder.PostAsJsonAsync(url, body, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Field.PostAsJsonAsync(url, body, CancellationToken), HttpStatusCode.Forbidden, "access.forbidden");
        await ApiAssert.Problem(await world.Other.PostAsJsonAsync(url, body, CancellationToken), HttpStatusCode.NotFound, "section.not_found");
        var sites = await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/grave-sites", CancellationToken);
        Assert.Equal(2, ApiAssert.Rows(sites).GetArrayLength());
    }

    [Fact]
    public async Task Replacing_an_outline_keeps_to_grave_limits()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var section = await OutlinedSection(world);
        var site = ApiAssert.Id(await Draw(world.Clerk, section, new { outline = Grave(0, 0), kind = "single" }));
        var url = $"/api/v1/grave-sites/{site}/outline";

        var rotated = await ApiAssert.Ok(await world.Clerk.PutAsJsonAsync(url, new { outline = Rotated(1, 1, 20) }, CancellationToken));
        await ApiAssert.Problem(await world.Clerk.PutAsJsonAsync(url, new { outline = Grave(0, 0, 15, 15) }, CancellationToken), HttpStatusCode.UnprocessableEntity, "grave_site.size_invalid");
        await ApiAssert.Problem(await world.Clerk.PutAsJsonAsync(url, new { outline = Grave(0, 0, 0.4, 0.4) }, CancellationToken), HttpStatusCode.UnprocessableEntity, "grave_site.size_invalid");
        await ApiAssert.Problem(await world.Clerk.PutAsJsonAsync(url, new { outline = Circle(0, 0, 1.5, 200) }, CancellationToken), HttpStatusCode.BadRequest, "grave_site.outline_too_complex");
        var circle = await ApiAssert.Ok(await world.Clerk.PutAsJsonAsync(url, new { outline = Circle(0, 0, 1.5, 199) }, CancellationToken));

        Assert.Equal(5, rotated.GetProperty("outline").GetArrayLength());
        Assert.Equal(200, circle.GetProperty("outline").GetArrayLength());
    }

    private static async Task<Guid> OutlinedSection(OfficeWorld world) =>
        await OfficeWorld.PostId(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/sections", new { name = "Drawn", code = "D", outline = OfficeWorld.Ring() }, CancellationToken);

    private static async Task<JsonElement> Draw(HttpClient client, Guid section, object body) =>
        await OfficeWorld.Post(client, $"/api/v1/sections/{section}/grave-sites/drawn", body, CancellationToken);

    private static async Task<string[]> Codes(HttpClient client, Guid cemetery)
    {
        var sites = await OfficeWorld.Get(client, $"/api/v1/cemeteries/{cemetery}/grave-sites", CancellationToken);
        return ApiAssert.Rows(sites).EnumerateArray().Select(Code).ToArray();
    }

    private static string Code(JsonElement site) => site.GetProperty("code").GetString()!;

    private static double MetersPerDegreeLongitude => MetersPerDegreeLatitude * Math.Cos(Latitude * Math.PI / 180);

    private static object Point(double east, double north) =>
        new { longitude = Longitude + east / MetersPerDegreeLongitude, latitude = Latitude + north / MetersPerDegreeLatitude };

    private static object[] Grave(double east, double north, double width = 1.2, double depth = 2.4) =>
    [
        Point(east, north),
        Point(east + width, north),
        Point(east + width, north + depth),
        Point(east, north + depth),
        Point(east, north),
    ];

    private static object[] LShape(double east, double north) =>
    [
        Point(east, north),
        Point(east + 3, north),
        Point(east + 3, north + 1),
        Point(east + 1, north + 1),
        Point(east + 1, north + 3),
        Point(east, north + 3),
        Point(east, north),
    ];

    private static object[] Rotated(double east, double north, double degrees)
    {
        var angle = degrees * Math.PI / 180;
        (double X, double Y)[] corners = [(0, 0), (1.2, 0), (1.2, 2.4), (0, 2.4), (0, 0)];
        return corners
            .Select(corner => Point(
                east + corner.X * Math.Cos(angle) - corner.Y * Math.Sin(angle),
                north + corner.X * Math.Sin(angle) + corner.Y * Math.Cos(angle)))
            .ToArray();
    }

    /// <summary>A circle as a closed ring of <paramref name="vertices"/> + 1 points.</summary>
    private static object[] Circle(double east, double north, double radius, int vertices)
    {
        var ring = Enumerable.Range(0, vertices)
            .Select(index => 2 * Math.PI * index / vertices)
            .Select(angle => Point(east + radius + radius * Math.Cos(angle), north + radius + radius * Math.Sin(angle)))
            .ToList();
        ring.Add(ring[0]);
        return [.. ring];
    }
}

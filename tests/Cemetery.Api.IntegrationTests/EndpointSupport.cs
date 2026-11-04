using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cemetery.Domain.Identity;

namespace Cemetery.Api.IntegrationTests;

internal static class ApiAssert
{
    public static async Task<JsonElement> Ok(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    public static async Task Problem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var actual = root.TryGetProperty("errorCode", out var errorCode)
            ? errorCode.GetString()
            : root.GetProperty("errors")[0].GetString();
        Assert.Equal(code, actual);
    }

    public static IEnumerable<Guid> Ids(JsonElement list)
    {
        foreach (var item in list.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var id))
                yield return id.GetGuid();
        }
    }

    public static Guid Id(JsonElement body) => body.GetProperty("id").GetGuid();

    public static Guid FirstId(JsonElement list) => list.EnumerateArray().First().GetProperty("id").GetGuid();
}

internal sealed class OfficeWorld
{
    public required HttpClient Clerk { get; init; }

    public required HttpClient Field { get; init; }

    public required HttpClient Holder { get; init; }

    public required HttpClient Other { get; init; }

    public Guid CemeteryId { get; init; }

    public Guid SectionId { get; init; }

    public Guid SiteId { get; init; }

    public Guid SecondSiteId { get; init; }

    public Guid DeceasedId { get; init; }

    public static async Task<OfficeWorld> Open(ApiFixture api, CancellationToken cancellationToken)
    {
        var label = Guid.NewGuid().ToString("N")[..8];
        var clerk = await api.SignedInClientAsync(MembershipRole.CemeteryClerk, label, cancellationToken);
        var field = await api.SignedInClientAsync(MembershipRole.FieldWorker, label, cancellationToken);
        var holder = await api.SignedInClientAsync(MembershipRole.GraveHolder, label, cancellationToken);
        var other = await api.SignedInClientAsync(MembershipRole.CemeteryClerk, label + "b", cancellationToken);
        var cemeteryId = await PostId(clerk, "/api/v1/cemeteries", new { name = "Pilot", schematic = true }, cancellationToken);
        var sectionId = await PostId(clerk, $"/api/v1/cemeteries/{cemeteryId}/sections", new { name = "Main", code = "S1" }, cancellationToken);
        var sites = await Post(clerk, $"/api/v1/cemeteries/{cemeteryId}/plan", new
        {
            kind = "single",
            rows = 1,
            columns = 2,
            originLongitude = 15.98,
            originLatitude = 45.81,
            widthMeters = 1.0,
            depthMeters = 2.0,
            gapMeters = 0.3,
            rotationDegrees = 0.0,
        }, cancellationToken);
        var deceasedId = await PostId(clerk, $"/api/v1/cemeteries/{cemeteryId}/deceased", new
        {
            givenName = "Ana",
            familyName = "Kovac",
            bornOn = "1920-01-02",
            diedOn = "2001-03-04",
        }, cancellationToken);
        return new OfficeWorld
        {
            Clerk = clerk,
            Field = field,
            Holder = holder,
            Other = other,
            CemeteryId = cemeteryId,
            SectionId = sectionId,
            SiteId = ApiAssert.FirstId(sites),
            SecondSiteId = sites.EnumerateArray().ElementAt(1).GetProperty("id").GetGuid(),
            DeceasedId = deceasedId,
        };
    }

    public static async Task<JsonElement> Post(HttpClient client, string url, object body, CancellationToken cancellationToken) =>
        await ApiAssert.Ok(await client.PostAsJsonAsync(url, body, cancellationToken));

    public static async Task<Guid> PostId(HttpClient client, string url, object body, CancellationToken cancellationToken) =>
        ApiAssert.Id(await Post(client, url, body, cancellationToken));

    public static async Task<JsonElement> Get(HttpClient client, string url, CancellationToken cancellationToken) =>
        await ApiAssert.Ok(await client.GetAsync(url, cancellationToken));

    public static object Ring() => new object[]
    {
        new { longitude = 16.0, latitude = 45.0 },
        new { longitude = 16.002, latitude = 45.0 },
        new { longitude = 16.002, latitude = 45.001 },
        new { longitude = 16.0, latitude = 45.001 },
        new { longitude = 16.0, latitude = 45.0 },
    };

    public static object Grid(int columns) => new
    {
        kind = "single",
        rows = 1,
        columns,
        originLongitude = 15.98,
        originLatitude = 45.81,
        widthMeters = 1.0,
        depthMeters = 2.0,
        gapMeters = 0.3,
        rotationDegrees = 0.0,
    };

    public static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string url, object? body, CancellationToken cancellationToken)
    {
        var message = new HttpRequestMessage(method, url);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return client.SendAsync(message, cancellationToken);
    }
}

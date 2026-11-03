using System.Net.Http.Json;

namespace Cemetery.Api.IntegrationTests;

public sealed partial class ApiFixture
{
    public async Task<Guid> SeedCemeteryAsync(HttpClient client, CancellationToken cancellationToken = default)
    {
        cemeteryCount++;
        var response = await client.PostAsJsonAsync("/api/v1/cemeteries", new
        {
            name = $"Cemetery {cemeteryCount}",
            schematic = true,
        }, cancellationToken);
        return await ReadIdAsync(response, cancellationToken);
    }

    public async Task<Guid> SeedGraveSiteAsync(HttpClient client, Guid cemeteryId, CancellationToken cancellationToken = default)
    {
        var sectionId = await AddSectionAsync(client, cemeteryId, cancellationToken);
        return await GenerateSiteAsync(client, sectionId, cancellationToken);
    }

    public async Task<Guid> SeedDeceasedAsync(HttpClient client, Guid cemeteryId, CancellationToken cancellationToken = default)
    {
        deceasedCount++;
        var response = await client.PostAsJsonAsync($"/api/v1/cemeteries/{cemeteryId}/deceased", new
        {
            givenName = "Ana",
            familyName = $"Kovac{deceasedCount}",
            bornOn = "1920-01-02",
            diedOn = "2001-03-04",
        }, cancellationToken);
        return await ReadIdAsync(response, cancellationToken);
    }

    private async Task<Guid> AddSectionAsync(HttpClient client, Guid cemeteryId, CancellationToken cancellationToken)
    {
        sectionCount++;
        var response = await client.PostAsJsonAsync($"/api/v1/cemeteries/{cemeteryId}/sections", new
        {
            name = "Main",
            code = $"S{sectionCount}",
        }, cancellationToken);
        return await ReadIdAsync(response, cancellationToken);
    }

    private static async Task<Guid> GenerateSiteAsync(HttpClient client, Guid sectionId, CancellationToken cancellationToken)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/sections/{sectionId}/grave-sites", new
        {
            kind = "single",
            rows = 1,
            columns = 1,
            originLongitude = 15.98,
            originLatitude = 45.81,
            widthMeters = 1.0,
            depthMeters = 2.0,
            gapMeters = 0.3,
            rotationDegrees = 0.0,
        }, cancellationToken);
        return await ReadFirstIdAsync(response, cancellationToken);
    }
}

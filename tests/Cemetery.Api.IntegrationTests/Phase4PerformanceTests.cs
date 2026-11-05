using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using Npgsql;

namespace Cemetery.Api.IntegrationTests;

public sealed class Phase4PerformanceTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task A_register_page_of_ten_thousand_returns_within_100_milliseconds()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await SeedDeceased(world, 10_000);
        var watch = Stopwatch.StartNew();
        var page = await OfficeWorld.Get(world.Clerk, $"/api/v1/cemeteries/{world.CemeteryId}/deceased?limit=50", CancellationToken);
        watch.Stop();
        Assert.Equal(50, ApiAssert.Rows(page).GetArrayLength());
        Assert.True(watch.ElapsedMilliseconds < 100, $"page took {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task A_five_thousand_site_map_is_small_and_cacheable()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        await SeedSites(world, 5_000);
        var watch = Stopwatch.StartNew();
        var response = await world.Clerk.GetAsync($"/api/v1/cemeteries/{world.CemeteryId}/map", CancellationToken);
        watch.Stop();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsByteArrayAsync(CancellationToken);
        Assert.True(watch.ElapsedMilliseconds < 300, $"map took {watch.ElapsedMilliseconds} ms");
        Assert.True(Gzip(body) < 1_500_000, $"gzipped map is {Gzip(body)} bytes");
        using var again = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/cemeteries/{world.CemeteryId}/map");
        again.Headers.TryAddWithoutValidation("If-None-Match", response.Headers.ETag!.Tag);
        var cached = await world.Clerk.SendAsync(again, CancellationToken);
        Assert.Equal(HttpStatusCode.NotModified, cached.StatusCode);
    }

    private async Task SeedDeceased(OfficeWorld world, int count)
    {
        await using var connection = new NpgsqlConnection(Api.OwnerConnection);
        await connection.OpenAsync(CancellationToken);
        var tenant = await TenantOf(connection, world.CemeteryId);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO cemetery.deceased (id, tenant_id, cemetery_id, given_name, family_name, version, recorded_at)
            SELECT gen_random_uuid(), @tenant, @cemetery, 'Ana', 'Row' || i, 1, now()
            FROM generate_series(1, @count) AS i
            """,
            connection);
        command.Parameters.AddWithValue("tenant", tenant);
        command.Parameters.AddWithValue("cemetery", world.CemeteryId);
        command.Parameters.AddWithValue("count", count);
        await command.ExecuteNonQueryAsync(CancellationToken);
    }

    private async Task SeedSites(OfficeWorld world, int count)
    {
        await using var connection = new NpgsqlConnection(Api.OwnerConnection);
        await connection.OpenAsync(CancellationToken);
        await using (var gate = new NpgsqlCommand("ALTER TABLE cemetery.grave_sites DISABLE TRIGGER grave_sites_no_overlap", connection))
            await gate.ExecuteNonQueryAsync(CancellationToken);
        try
        {
            await InsertSites(connection, world, count);
        }
        finally
        {
            await using var gate = new NpgsqlCommand("ALTER TABLE cemetery.grave_sites ENABLE TRIGGER grave_sites_no_overlap", connection);
            await gate.ExecuteNonQueryAsync(CancellationToken);
        }
    }

    private static async Task InsertSites(NpgsqlConnection connection, OfficeWorld world, int count)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO cemetery.grave_sites (
              id, tenant_id, cemetery_id, section_id, row_id, code, kind, capacity, outline, closed, hidden_from_public, used, status, updated_at)
            SELECT gen_random_uuid(), tenant_id, cemetery_id, section_id, row_id, 'P' || i, kind, capacity,
                   ST_Translate(outline, i * 0.003, 0), false, false, 0, 'available', now()
            FROM cemetery.grave_sites
            CROSS JOIN generate_series(1, @count) AS i
            WHERE id = @site
            """,
            connection);
        command.Parameters.AddWithValue("site", world.SiteId);
        command.Parameters.AddWithValue("count", count);
        await command.ExecuteNonQueryAsync(CancellationToken);
    }

    private static async Task<Guid> TenantOf(NpgsqlConnection connection, Guid cemeteryId)
    {
        await using var command = new NpgsqlCommand("SELECT tenant_id FROM cemetery.cemeteries WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", cemeteryId);
        return (Guid)(await command.ExecuteScalarAsync(CancellationToken))!;
    }

    private static int Gzip(byte[] body)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            gzip.Write(body, 0, body.Length);
        return (int)output.Length;
    }
}

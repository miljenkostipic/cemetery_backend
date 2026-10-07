using System.Data;
using System.Data.Common;
using System.Text.Json;
using Cemetery.Application.Abstractions;
using Cemetery.Application.Catalog;
using Cemetery.Application.Layout;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Infrastructure.Persistence;

internal sealed class PublicCatalogRepository(CemeteryDbContext db, ISessionHints hints) : IPublicCatalog
{
    public Task<IReadOnlyList<PublicCemeterySummary>> ListCemeteriesAsync(CancellationToken cancellationToken) =>
        Read(ListAsync, cancellationToken);

    public Task<IReadOnlyList<PublicSearchHit>> SearchAsync(
        string name,
        int? yearFrom,
        int? yearTo,
        string? slug,
        DateOnly today,
        CancellationToken cancellationToken) =>
        Read(token => SearchQueryAsync(name, yearFrom, yearTo, slug, today, token), cancellationToken);

    public Task<PublicMemorial?> FindMemorialAsync(Guid deceasedId, DateOnly today, CancellationToken cancellationToken) =>
        Read(token => MemorialAsync(deceasedId, today, token), cancellationToken);

    public Task<PublicMap?> FindMapAsync(string slug, Guid cemeteryId, CancellationToken cancellationToken) =>
        Read(token => MapAsync(slug, cemeteryId, token), cancellationToken);

    private async Task<T> Read<T>(Func<CancellationToken, Task<T>> query, CancellationToken cancellationToken)
    {
        hints.PublicRead = true;
        try
        {
            return await query(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            hints.PublicRead = false;
        }
    }

    private async Task<IReadOnlyList<PublicCemeterySummary>> ListAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT organization_slug, organization_name, cemetery_id, cemetery_name, schematic
            FROM cemetery.public_cemeteries()
            """;
        return await QueryAsync(sql, CatalogRows.Cemetery, cancellationToken).ConfigureAwait(false);
    }

    private Task<IReadOnlyList<PublicSearchHit>> SearchQueryAsync(
        string name,
        int? yearFrom,
        int? yearTo,
        string? slug,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT deceased_id, given_name, family_name, born_on, died_on, organization_slug, organization_name, cemetery_id, cemetery_name, grave_site_id, grave_site_code
            FROM cemetery.public_search(@name, @yearFrom, @yearTo, @slug, @today)
            """;
        return QueryAsync(
            sql,
            CatalogRows.Hit,
            cancellationToken,
            Parameter("name", name),
            Parameter("yearFrom", yearFrom),
            Parameter("yearTo", yearTo),
            Parameter("slug", slug),
            Parameter("today", today));
    }

    private async Task<PublicMemorial?> MemorialAsync(Guid deceasedId, DateOnly today, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT deceased_id, given_name, family_name, born_on, died_on, epitaph, photo_url, organization_slug, organization_name, cemetery_id, cemetery_name, schematic, grave_site_id, grave_site_code, outline_json
            FROM cemetery.public_memorial(@deceasedId, @today)
            """;
        var rows = await QueryAsync(sql, CatalogRows.Memorial, cancellationToken, Parameter("deceasedId", deceasedId), Parameter("today", today)).ConfigureAwait(false);
        return rows.Count == 0 ? null : rows[0];
    }

    private async Task<PublicMap?> MapAsync(string slug, Guid cemeteryId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT organization_slug, organization_name, cemetery_id, cemetery_name, schematic, plan_image_url, plan_json
            FROM cemetery.public_place(@slug, @cemeteryId)
            """;
        var places = await QueryAsync(sql, CatalogRows.Place, cancellationToken, Parameter("slug", slug), Parameter("cemeteryId", cemeteryId)).ConfigureAwait(false);
        if (places.Count == 0)
            return null;
        var sections = await SectionsAsync(slug, cemeteryId, cancellationToken).ConfigureAwait(false);
        var sites = await SitesAsync(slug, cemeteryId, cancellationToken).ConfigureAwait(false);
        var place = places[0];
        return new PublicMap(place.OrganizationSlug, place.OrganizationName, place.CemeteryId, place.CemeteryName, place.Schematic, place.PlanImageUrl, GeoJsonRing.Parse(place.PlanJson), sections, sites);
    }

    private async Task<PublicSection[]> SectionsAsync(string slug, Guid cemeteryId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, name, code, outline_json FROM cemetery.public_sections(@slug, @cemeteryId)
            """;
        var rows = await QueryAsync(sql, CatalogRows.Section, cancellationToken, Parameter("slug", slug), Parameter("cemeteryId", cemeteryId)).ConfigureAwait(false);
        return rows.ToArray();
    }

    private async Task<PublicSite[]> SitesAsync(string slug, Guid cemeteryId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, code, outline_json FROM cemetery.public_sites(@slug, @cemeteryId)
            """;
        var rows = await QueryAsync(sql, CatalogRows.Site, cancellationToken, Parameter("slug", slug), Parameter("cemeteryId", cemeteryId)).ConfigureAwait(false);
        return rows.ToArray();
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<DbDataReader, T> map, CancellationToken cancellationToken, params DbParameter[] parameters)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
            command.Parameters.Add(parameter);
        var rows = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(map(reader));
        return rows;
    }

    private DbParameter Parameter(string name, object? value)
    {
        var parameter = db.Database.GetDbConnection().CreateCommand().CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }
}

file static class CatalogRows
{
    public static PublicCemeterySummary Cemetery(DbDataReader reader) =>
        new(reader.GetString(0), reader.GetString(1), reader.GetGuid(2), reader.GetString(3), reader.GetBoolean(4));

    public static PublicSearchHit Hit(DbDataReader reader) =>
        new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), Date(reader, 3), Date(reader, 4), reader.GetString(5), reader.GetString(6), reader.GetGuid(7), reader.GetString(8), GuidOrNull(reader, 9), Text(reader, 10));

    public static PublicMemorial Memorial(DbDataReader reader) =>
        new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), Date(reader, 3), Date(reader, 4), Text(reader, 5), Text(reader, 6), reader.GetString(7), reader.GetString(8), reader.GetGuid(9), reader.GetString(10), reader.GetBoolean(11), GuidOrNull(reader, 12), Text(reader, 13), GeoJsonRing.Parse(Text(reader, 14)));

    public static PlaceRow Place(DbDataReader reader) =>
        new(reader.GetString(0), reader.GetString(1), reader.GetGuid(2), reader.GetString(3), reader.GetBoolean(4), Text(reader, 5), Text(reader, 6));

    public static PublicSection Section(DbDataReader reader) =>
        new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), GeoJsonRing.Parse(Text(reader, 3)));

    public static PublicSite Site(DbDataReader reader) =>
        new(reader.GetGuid(0), reader.GetString(1), GeoJsonRing.Parse(Text(reader, 2)));

    private static string? Text(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static Guid? GuidOrNull(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);

    private static DateOnly? Date(DbDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetFieldValue<DateOnly>(ordinal);
}

file sealed record PlaceRow(string OrganizationSlug, string OrganizationName, Guid CemeteryId, string CemeteryName, bool Schematic, string? PlanImageUrl, string? PlanJson);

file static class GeoJsonRing
{
    public static GeoPointView[] Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            return Points(document.RootElement);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static GeoPointView[] Points(JsonElement root)
    {
        if (!root.TryGetProperty("coordinates", out var coordinates) || coordinates.GetArrayLength() == 0)
            return [];
        var ring = coordinates[0];
        var points = new List<GeoPointView>();
        foreach (var position in ring.EnumerateArray())
        {
            if (position.GetArrayLength() < 2)
                continue;
            points.Add(new GeoPointView(position[0].GetDouble(), position[1].GetDouble()));
        }

        return points.ToArray();
    }
}

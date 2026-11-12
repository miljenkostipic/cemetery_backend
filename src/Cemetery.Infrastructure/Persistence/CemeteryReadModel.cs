using System.Data;
using System.Globalization;
using Cemetery.Application.Abstractions;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Infrastructure.Persistence;

internal sealed class CemeteryReadModel(CemeteryDbContext db) : ICemeteryReadModel
{
    public async Task<(string Etag, string Json)> MapAsync(Guid cemeteryId, CancellationToken cancellationToken)
    {
        var row = await ReadAsync(MapSql, Parameter("id", cemeteryId), cancellationToken).ConfigureAwait(false);
        var stamp = row.Count == 0 ? "0" : row[0].Stamp ?? "0";
        var json = row.Count == 0 ? EmptyMap : row[0].Json ?? EmptyMap;
        return ("W/\"" + stamp + "\"", json);
    }

    public async Task<IReadOnlyList<SearchHit>> SearchAsync(string query, Guid? cemeteryId, bool includeHolders, CancellationToken cancellationToken)
    {
        var rows = await HitsAsync(query, cemeteryId, includeHolders, cancellationToken).ConfigureAwait(false);
        return rows;
    }

    public Task AddEntranceAsync(Entrance entrance, CancellationToken cancellationToken)
    {
        db.Entrances.Add(entrance);
        return Task.CompletedTask;
    }

    public Task<Entrance?> FindEntranceAsync(Guid id, CancellationToken cancellationToken) =>
        db.Entrances.FirstOrDefaultAsync(entrance => entrance.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Entrance>> ListEntrancesAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.Entrances.Where(entrance => entrance.CemeteryId == cemeteryId).OrderBy(entrance => entrance.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task RemoveEntranceAsync(Entrance entrance, CancellationToken cancellationToken)
    {
        db.Entrances.Remove(entrance);
        return Task.CompletedTask;
    }

    public Task AddPathAsync(CemeteryPath path, CancellationToken cancellationToken)
    {
        db.Paths.Add(path);
        return Task.CompletedTask;
    }

    public Task<CemeteryPath?> FindPathAsync(Guid id, CancellationToken cancellationToken) =>
        db.Paths.FirstOrDefaultAsync(path => path.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CemeteryPath>> ListPathsAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.Paths.Where(path => path.CemeteryId == cemeteryId).OrderBy(path => path.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task RemovePathAsync(CemeteryPath path, CancellationToken cancellationToken)
    {
        db.Paths.Remove(path);
        return Task.CompletedTask;
    }

    public Task<WorkOrderPhoto?> LatestPhotoAsync(Guid graveSiteId, CancellationToken cancellationToken) =>
        db.WorkOrderPhotos.Where(photo => db.WorkOrders.Any(order => order.Id == photo.WorkOrderId && order.GraveSiteId == graveSiteId && order.Status == WorkOrderStatus.Completed))
            .OrderByDescending(photo => photo.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<WorkOrderPhoto?> FindPhotoAsync(Guid workOrderId, Guid photoId, CancellationToken cancellationToken) =>
        db.WorkOrderPhotos.FirstOrDefaultAsync(photo => photo.Id == photoId && photo.WorkOrderId == workOrderId, cancellationToken);

    public async Task<IReadOnlyList<WorkOrder>> ListOpenWorkOrdersAsync(Guid graveSiteId, CancellationToken cancellationToken) =>
        await db.WorkOrders.Where(order => order.GraveSiteId == graveSiteId && order.Status == WorkOrderStatus.Open).ToListAsync(cancellationToken).ConfigureAwait(false);

    private async Task<IReadOnlyList<SearchHit>> HitsAsync(string query, Guid? cemeteryId, bool includeHolders, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SearchSql;
        command.Parameters.Add(Parameter("q", query));
        command.Parameters.Add(Parameter("cemetery", cemeteryId));
        command.Parameters.Add(Parameter("holders", includeHolders));
        var hits = new List<SearchHit>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            hits.Add(new SearchHit(reader.GetString(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetDouble(4), reader.IsDBNull(5) ? null : reader.GetDouble(5)));
        return hits;
    }

    private async Task<List<MapRow>> ReadAsync(string sql, System.Data.Common.DbParameter parameter, CancellationToken cancellationToken)
    {
        var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.Add(parameter);
        var rows = new List<MapRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(new MapRow(reader.IsDBNull(0) ? null : reader.GetFieldValue<DateTimeOffset>(0).UtcTicks.ToString(CultureInfo.InvariantCulture), reader.IsDBNull(1) ? null : reader.GetString(1)));
        return rows;
    }

    private async Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var session = connection.CreateCommand();
        session.CommandText = "SELECT set_config('app.tenant_id', @tenant, false)";
        var tenant = session.CreateParameter();
        tenant.ParameterName = "tenant";
        tenant.Value = db.CurrentTenant == Guid.Empty ? "" : db.CurrentTenant.ToString();
        session.Parameters.Add(tenant);
        await session.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private System.Data.Common.DbParameter Parameter(string name, object? value)
    {
        var parameter = db.Database.GetDbConnection().CreateCommand().CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        return parameter;
    }

    private const string EmptyMap = """{"type":"FeatureCollection","features":[],"counts":{}}""";

    private const string MapSql = """
        SELECT COALESCE(MAX(updated_at), TIMESTAMPTZ 'epoch') AS updated_at,
               '{"type":"FeatureCollection","features":[' || COALESCE(string_agg(feature, ','), '') || '],"counts":' ||
               (SELECT COALESCE(json_object_agg(status, total), '{}'::json)::text
                FROM (SELECT status, COUNT(*)::int AS total FROM cemetery.grave_sites WHERE cemetery_id = @id GROUP BY status) AS grouped)
               || '}'
        FROM (
          SELECT updated_at,
                 '{"type":"Feature","id":' || to_json(id)::text || ',"geometry":' || COALESCE(ST_AsGeoJSON(outline), 'null')
                 || ',"properties":{"layer":"section","id":' || to_json(id)::text || ',"name":' || to_json(name)::text || ',"code":' || to_json(code)::text || '}}' AS feature
          FROM cemetery.sections WHERE cemetery_id = @id
          UNION ALL
          SELECT updated_at,
                 '{"type":"Feature","id":' || to_json(id)::text || ',"geometry":' || COALESCE(ST_AsGeoJSON(outline), 'null')
                 || ',"properties":{"layer":"site","id":' || to_json(id)::text || ',"sectionId":' || to_json(section_id)::text || ',"code":' || to_json(code)::text
                 || ',"kind":' || to_json(kind)::text || ',"status":' || to_json(status)::text
                 || ',"used":' || used::text || ',"capacity":' || capacity::text
                 || ',"hidden":' || CASE WHEN hidden_from_public THEN 'true' ELSE 'false' END || '}}' AS feature
          FROM cemetery.grave_sites WHERE cemetery_id = @id
          UNION ALL
          SELECT updated_at,
                 '{"type":"Feature","id":' || to_json(id)::text || ',"geometry":' || ST_AsGeoJSON(location)
                 || ',"properties":{"layer":"entrance","name":' || to_json(name)::text || '}}' AS feature
          FROM cemetery.entrances WHERE cemetery_id = @id
          UNION ALL
          SELECT updated_at,
                 '{"type":"Feature","id":' || to_json(id)::text || ',"geometry":' || ST_AsGeoJSON(line)
                 || ',"properties":{"layer":"path","name":' || to_json(name)::text || '}}' AS feature
          FROM cemetery.paths WHERE cemetery_id = @id
        ) AS rows
        """;

    private const string SearchSql = """
        SELECT kind, id, label, site_id, longitude, latitude
        FROM (
          SELECT 'deceased' AS kind, person.id, person.given_name || ' ' || person.family_name AS label, burial.grave_site_id AS site_id,
                 ST_X(ST_Centroid(site.outline)) AS longitude, ST_Y(ST_Centroid(site.outline)) AS latitude
          FROM cemetery.deceased AS person
          LEFT JOIN cemetery.interments AS burial ON burial.deceased_id = person.id AND burial.exhumed_on IS NULL AND burial.transferred_on IS NULL AND burial.superseded_on IS NULL
          LEFT JOIN cemetery.grave_sites AS site ON site.id = burial.grave_site_id
          WHERE person.removed_at IS NULL
            AND (@cemetery IS NULL OR person.cemetery_id = @cemetery)
            AND cemetery.fold_text_immutable(person.given_name || ' ' || person.family_name) LIKE '%' || cemetery.fold_text_immutable(@q) || '%'
          UNION ALL
          SELECT 'site', site.id, site.code, site.id, ST_X(ST_Centroid(site.outline)), ST_Y(ST_Centroid(site.outline))
          FROM cemetery.grave_sites AS site
          WHERE (@cemetery IS NULL OR site.cemetery_id = @cemetery)
            AND cemetery.fold_text_immutable(site.code) LIKE '%' || cemetery.fold_text_immutable(@q) || '%'
          UNION ALL
          SELECT 'holder', holder.id, holder.given_name || ' ' || holder.family_name, right_row.grave_site_id,
                 ST_X(ST_Centroid(site.outline)), ST_Y(ST_Centroid(site.outline))
          FROM cemetery.holders AS holder
          LEFT JOIN cemetery.grave_right_shares AS share ON share.holder_id = holder.id
          LEFT JOIN cemetery.grave_rights AS right_row ON right_row.id = share.grave_right_id AND right_row.status = 'active'
          LEFT JOIN cemetery.grave_sites AS site ON site.id = right_row.grave_site_id
          WHERE @holders
            AND (@cemetery IS NULL OR site.cemetery_id = @cemetery OR site.id IS NULL)
            AND cemetery.fold_text_immutable(holder.given_name || ' ' || holder.family_name) LIKE '%' || cemetery.fold_text_immutable(@q) || '%'
        ) AS hits
        LIMIT 20
        """;

    private sealed record MapRow(string? Stamp, string? Json);
}

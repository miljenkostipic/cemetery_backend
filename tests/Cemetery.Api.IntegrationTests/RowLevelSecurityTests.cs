using Npgsql;

namespace Cemetery.Api.IntegrationTests;

public sealed class RowLevelSecurityTests(ApiFixture api) : ApiTest(api)
{
    [Fact]
    public async Task Every_tenant_table_hides_other_rows()
    {
        var world = await OfficeWorld.Open(Api, CancellationToken);
        var alpha = (await OfficeWorld.Get(world.Clerk, "/api/v1/dashboard", CancellationToken)).GetProperty("organizationId").GetGuid();
        var beta = (await OfficeWorld.Get(world.Other, "/api/v1/dashboard", CancellationToken)).GetProperty("organizationId").GetGuid();
        var tables = await TablesAsync();
        Assert.NotEmpty(tables);

        await using var app = new NpgsqlConnection(AppConnection());
        await app.OpenAsync(CancellationToken);
        foreach (var table in tables)
        {
            await using (var clear = new NpgsqlCommand("SELECT set_config('app.tenant_id', '', false)", app))
                _ = await clear.ExecuteScalarAsync(CancellationToken);
            await using var count = new NpgsqlCommand($"SELECT count(*) FROM cemetery.{table}", app);
            Assert.Equal(0L, (long)(await count.ExecuteScalarAsync(CancellationToken) ?? 0L));

            await using (var use = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, false)", app))
            {
                use.Parameters.AddWithValue("tenant", alpha.ToString());
                _ = await use.ExecuteScalarAsync(CancellationToken);
            }

            await using var foreign = new NpgsqlCommand($"SELECT count(*) FROM cemetery.{table} WHERE tenant_id = @beta", app);
            foreign.Parameters.AddWithValue("beta", beta);
            Assert.Equal(0L, (long)(await foreign.ExecuteScalarAsync(CancellationToken) ?? 0L));
        }
    }

    private async Task<IReadOnlyList<string>> TablesAsync()
    {
        await using var owner = new NpgsqlConnection(Api.OwnerConnection);
        await owner.OpenAsync(CancellationToken);
        const string sql = """
            SELECT columns.table_name
            FROM information_schema.columns AS columns
            JOIN pg_class AS relation ON relation.relname = columns.table_name
            JOIN pg_namespace AS schema ON schema.oid = relation.relnamespace AND schema.nspname = columns.table_schema
            WHERE columns.table_schema = 'cemetery'
              AND columns.column_name = 'tenant_id'
              AND relation.relkind = 'r'
            ORDER BY columns.table_name
            """;
        var names = new List<string>();
        await using var command = new NpgsqlCommand(sql, owner);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken);
        while (await reader.ReadAsync(CancellationToken))
        {
            var name = reader.GetString(0);
            Assert.Matches("^[a-z_]+$", name);
            names.Add(name);
        }

        await reader.CloseAsync();
        foreach (var name in names)
        {
            await using var flags = new NpgsqlCommand("""
                SELECT relation.relrowsecurity, relation.relforcerowsecurity
                FROM pg_class AS relation
                JOIN pg_namespace AS schema ON schema.oid = relation.relnamespace
                WHERE schema.nspname = 'cemetery' AND relation.relname = @name
                """, owner);
            flags.Parameters.AddWithValue("name", name);
            await using var row = await flags.ExecuteReaderAsync(CancellationToken);
            Assert.True(await row.ReadAsync(CancellationToken));
            Assert.True(row.GetBoolean(0), name);
            Assert.True(row.GetBoolean(1), name);
        }

        return names;
    }

    private string AppConnection()
    {
        var builder = new NpgsqlConnectionStringBuilder(Api.OwnerConnection)
        {
            Username = "cemetery_app",
            Password = "cemetery_app",
        };
        return builder.ConnectionString;
    }
}

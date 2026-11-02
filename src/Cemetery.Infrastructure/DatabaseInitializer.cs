using Cemetery.Infrastructure.Persistence;
using Cemetery.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace Cemetery.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task ApplyAsync(string ownerConnection, string appConnection, CancellationToken cancellationToken)
    {
        var password = new NpgsqlConnectionStringBuilder(appConnection).Password
            ?? throw new InvalidOperationException("App connection password is required.");
        await EnsureRoleAsync(ownerConnection, password, cancellationToken).ConfigureAwait(false);
        await AdoptLegacyHistoryAsync(ownerConnection, cancellationToken).ConfigureAwait(false);
        await using var db = new CemeteryDbContext(CreateOptions(ownerConnection), new AmbientTenant());
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static void Configure(NpgsqlDbContextOptionsBuilder npgsql) =>
        npgsql.UseNetTopologySuite().MigrationsHistoryTable("__ef_migrations", "cemetery");

    internal static DbContextOptions<CemeteryDbContext> CreateOptions(string ownerConnection) =>
        new DbContextOptionsBuilder<CemeteryDbContext>()
            .UseNpgsql(ownerConnection, Configure)
            .UseSnakeCaseNamingConvention()
            .Options;

    private static async Task EnsureRoleAsync(string ownerConnection, string password, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ownerConnection);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var quoted = await QuoteAsync(connection, password, cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(RoleSql(quoted), connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> QuoteAsync(NpgsqlConnection connection, string password, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT quote_literal(@password)", connection);
        command.Parameters.AddWithValue("password", password);
        return (string)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
    }

    private static async Task AdoptLegacyHistoryAsync(string ownerConnection, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ownerConnection);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(LegacyHistorySql(), connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string LegacyHistorySql() => """
        DO $body$
        BEGIN
          IF to_regclass('public."__EFMigrationsHistory"') IS NULL THEN RETURN; END IF;
          CREATE SCHEMA IF NOT EXISTS cemetery;
          CREATE TABLE IF NOT EXISTS cemetery.__ef_migrations (
            migration_id character varying(150) NOT NULL,
            product_version character varying(32) NOT NULL,
            CONSTRAINT pk___ef_migrations PRIMARY KEY (migration_id));
          INSERT INTO cemetery.__ef_migrations (migration_id, product_version)
          SELECT legacy.migration_id, legacy.product_version
          FROM public."__EFMigrationsHistory" AS legacy
          WHERE NOT EXISTS (
            SELECT 1 FROM cemetery.__ef_migrations AS existing
            WHERE existing.migration_id = legacy.migration_id);
          DROP TABLE public."__EFMigrationsHistory";
        END
        $body$;
        """;

    private static string RoleSql(string quotedPassword) => $"""
        DO $body$
        BEGIN
          IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'cemetery_app') THEN
            CREATE ROLE cemetery_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD {quotedPassword};
          ELSE
            ALTER ROLE cemetery_app WITH LOGIN PASSWORD {quotedPassword};
          END IF;
        END
        $body$;
        """;
}

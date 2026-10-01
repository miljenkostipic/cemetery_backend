using Cemetery.Infrastructure.Persistence;
using Cemetery.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cemetery.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task ApplyAsync(string ownerConnection, string appConnection, CancellationToken cancellationToken)
    {
        var password = new NpgsqlConnectionStringBuilder(appConnection).Password
            ?? throw new InvalidOperationException("App connection password is required.");
        await EnsureRoleAsync(ownerConnection, password, cancellationToken).ConfigureAwait(false);
        var options = new DbContextOptionsBuilder<CemeteryDbContext>()
            .UseNpgsql(ownerConnection, npgsql => npgsql.UseNetTopologySuite())
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new CemeteryDbContext(options, new AmbientTenant());
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

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

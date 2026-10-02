using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cemetery.Infrastructure.Persistence;

internal static class UniqueViolation
{
    private const string UniqueSqlState = "23505";

    public static string? ToErrorCode(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException postgres || postgres.SqlState != UniqueSqlState)
            return null;

        return postgres.ConstraintName switch
        {
            "organizations_slug_key" => "organization.slug_taken",
            "memberships_tenant_user_key" => "membership.already_exists",
            _ => "conflict.unique",
        };
    }
}

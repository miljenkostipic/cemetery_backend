using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cemetery.Infrastructure.Persistence;

internal static class UniqueViolation
{
    private const string UniqueSqlState = "23505";
    private const string CheckSqlState = "23514";

    public static string? ToErrorCode(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException postgres)
            return null;

        if (postgres.SqlState == CheckSqlState && postgres.MessageText.Contains("grave_site.overlaps", StringComparison.Ordinal))
            return "grave_site.overlaps";

        if (postgres.SqlState != UniqueSqlState)
            return null;

        return postgres.ConstraintName switch
        {
            "organizations_slug_key" => "organization.slug_taken",
            "memberships_tenant_user_key" => "membership.already_exists",
            "sections_tenant_code_key" => "section.code_taken",
            "grave_rows_tenant_label_key" => "row.label_taken",
            "grave_sites_tenant_code_key" => "grave_site.code_taken",
            _ => "conflict.unique",
        };
    }
}

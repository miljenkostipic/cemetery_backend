using System.Data.Common;
using Cemetery.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cemetery.Infrastructure.Persistence;

public sealed class TenantSessionInterceptor(
    ITenantContext tenant,
    ICurrentUser current,
    ISessionHints hints) : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Apply(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await ApplyAsync(command, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        Apply(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        await ApplyAsync(command, cancellationToken).ConfigureAwait(false);
        return result;
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Apply(command);
        return result;
    }

    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        await ApplyAsync(command, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private void Apply(DbCommand command) => ApplyAsync(command, CancellationToken.None).GetAwaiter().GetResult();

    private async Task ApplyAsync(DbCommand command, CancellationToken cancellationToken)
    {
        if (command.Connection is null || command.CommandText.Contains("set_config", StringComparison.Ordinal))
            return;

        await using var session = command.Connection.CreateCommand();
        session.CommandText = """
            SELECT set_config('app.tenant_id', @tenant, false),
                   set_config('app.user_id', @user, false),
                   set_config('app.invitation_hash', @hash, false)
            """;
        Add(session, "tenant", tenant.OrganizationId?.ToString() ?? "");
        Add(session, "user", current.UserId?.ToString() ?? "");
        Add(session, "hash", hints.InvitationHash ?? "");
        await session.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void Add(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

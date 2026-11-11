using Cemetery.Application.Abstractions;
using Cemetery.Domain.Integrations;

namespace Cemetery.Application.Integrations;

public sealed class OutboxDispatcher(
    IOutboxStore store,
    IOutboxHandler handler,
    ITenantContext tenant,
    ISessionHints hints,
    TimeProvider clock)
{
    public async Task<int> DispatchAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await Due(now, cancellationToken).ConfigureAwait(false);
        var delivered = 0;
        foreach (var message in due)
            delivered += await Deliver(message, now, cancellationToken).ConfigureAwait(false);

        return delivered;
    }

    private async Task<IReadOnlyList<OutboxMessage>> Due(DateTimeOffset now, CancellationToken cancellationToken)
    {
        hints.OutboxDispatch = true;
        try
        {
            return await store.ListDueAsync(now, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            hints.OutboxDispatch = false;
        }
    }

    private async Task<int> Deliver(OutboxMessage message, DateTimeOffset now, CancellationToken cancellationToken)
    {
        tenant.Use(message.TenantId);
        message.BeginAttempt();
        try
        {
            await handler.HandleAsync(message, cancellationToken).ConfigureAwait(false);
            message.MarkProcessed(now);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            message.MarkFailed(exception.Message);
        }

        await store.SaveAsync(cancellationToken).ConfigureAwait(false);
        return message.ProcessedAt is null ? 0 : 1;
    }
}

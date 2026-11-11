using Cemetery.Application.Abstractions;
using Cemetery.Domain.Integrations;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Infrastructure.Persistence;

internal sealed class OutboxStore(CemeteryDbContext db) : IOutboxStore
{
    private const int BatchSize = 50;

    public async Task<IReadOnlyList<OutboxMessage>> ListDueAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var waiting = await db.OutboxMessages.IgnoreQueryFilters()
            .Where(message => message.ProcessedAt == null && message.Attempts < OutboxMessage.MaxAttempts)
            .OrderBy(message => message.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return waiting.Where(message => message.IsDue(now)).ToArray();
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

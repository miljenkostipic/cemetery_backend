using Cemetery.Domain.Identity;
using Cemetery.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Cemetery.Infrastructure.Persistence;

public sealed class OutboxInterceptor(TimeProvider clock) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Write(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Write(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Write(DbContext? context)
    {
        if (context is not null)
            OutboxCapture.Write(context, clock.GetUtcNow());
    }
}

internal static class OutboxCapture
{
    public static void Write(DbContext context, DateTimeOffset now)
    {
        var raised = context.ChangeTracker.Entries<TenantEntity>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToArray();
        foreach (var entity in raised)
            Add(context, entity, now);
    }

    private static void Add(DbContext context, TenantEntity entity, DateTimeOffset now)
    {
        foreach (var domainEvent in entity.DomainEvents)
            context.Add(OutboxMessage.Capture(entity.TenantId, domainEvent, now));

        entity.ClearDomainEvents();
    }
}

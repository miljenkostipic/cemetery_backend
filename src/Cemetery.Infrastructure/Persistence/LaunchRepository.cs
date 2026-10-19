using Cemetery.Application.Abstractions;
using Cemetery.Domain.Launch;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Infrastructure.Persistence;

internal sealed class WebhookRepository(CemeteryDbContext db) : IWebhookRepository
{
    public Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken)
    {
        db.WebhookSubscriptions.Add(subscription);
        return Task.CompletedTask;
    }

    public Task<WebhookSubscription?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.WebhookSubscriptions.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WebhookSubscription>> ListAsync(CancellationToken cancellationToken) =>
        await db.WebhookSubscriptions.OrderBy(entity => entity.Url).ThenBy(entity => entity.Event).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Remove(WebhookSubscription subscription) => db.WebhookSubscriptions.Remove(subscription);

    public async Task EnqueueAsync(string eventName, Guid subjectId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var matches = await db.WebhookSubscriptions.Where(entity => entity.Event == eventName).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var subscription in matches)
            db.WebhookDeliveries.Add(WebhookDelivery.Queue(subscription.TenantId, subscription.Id, eventName, subjectId, at));
    }
}

internal sealed class MaticaLinkRepository(CemeteryDbContext db) : IMaticaLinkRepository
{
    public Task AddAsync(MaticaLink link, CancellationToken cancellationToken)
    {
        db.MaticaLinks.Add(link);
        return Task.CompletedTask;
    }

    public Task<MaticaLink?> FindByIntermentAsync(Guid intermentId, CancellationToken cancellationToken) =>
        db.MaticaLinks.FirstOrDefaultAsync(entity => entity.IntermentId == intermentId, cancellationToken);

    public Task<MaticaLink?> FindByCertificateAsync(Guid deathCertificateId, CancellationToken cancellationToken) =>
        db.MaticaLinks.FirstOrDefaultAsync(entity => entity.DeathCertificateId == deathCertificateId, cancellationToken);

    public async Task<IReadOnlyList<MaticaLink>> ListByCemeteryAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.MaticaLinks.Where(entity => entity.CemeteryId == cemeteryId).OrderBy(entity => entity.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
}

internal sealed class GdprExportRepository(CemeteryDbContext db) : IGdprExportRepository
{
    public Task AddAsync(GdprExport request, CancellationToken cancellationToken)
    {
        db.GdprExports.Add(request);
        return Task.CompletedTask;
    }
}

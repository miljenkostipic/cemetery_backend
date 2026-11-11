using Cemetery.Domain.Launch;

namespace Cemetery.Application.Abstractions;

public interface IWebhookRepository
{
    Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken);

    Task<WebhookSubscription?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<WebhookSubscription>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<WebhookSubscription>> ListByEventAsync(string eventName, CancellationToken cancellationToken);

    void Remove(WebhookSubscription subscription);

    void AddDelivery(WebhookDelivery delivery);

    Task<bool> WasDeliveredAsync(Guid subscriptionId, Guid subjectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WebhookDelivery>> ListDeliveriesAsync(CancellationToken cancellationToken);
}

public interface IMaticaLinkRepository
{
    Task AddAsync(MaticaLink link, CancellationToken cancellationToken);

    Task<MaticaLink?> FindByIntermentAsync(Guid intermentId, CancellationToken cancellationToken);

    Task<MaticaLink?> FindByCertificateAsync(Guid deathCertificateId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MaticaLink>> ListByCemeteryAsync(Guid cemeteryId, CancellationToken cancellationToken);
}

public interface IGdprExportRepository
{
    Task AddAsync(GdprExport request, CancellationToken cancellationToken);
}

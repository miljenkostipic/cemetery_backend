using Cemetery.Domain.Launch;

namespace Cemetery.Application.Abstractions;

public interface IWebhookRepository
{
    Task AddAsync(WebhookSubscription subscription, CancellationToken cancellationToken);

    Task<WebhookSubscription?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<WebhookSubscription>> ListAsync(CancellationToken cancellationToken);

    void Remove(WebhookSubscription subscription);

    Task EnqueueAsync(string eventName, Guid subjectId, DateTimeOffset at, CancellationToken cancellationToken);
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

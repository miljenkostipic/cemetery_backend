using Cemetery.Domain.Billing;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Operations;
using Cemetery.Domain.Rights;

namespace Cemetery.Application.Abstractions;

public interface IFileStorage
{
    Task<string> PutAsync(Guid tenantId, byte[] content, CancellationToken cancellationToken);

    Task<byte[]?> OpenAsync(string key, CancellationToken cancellationToken);

    Task DeleteAsync(string key, CancellationToken cancellationToken);
}

public interface IVirusScan
{
    Task ScanAsync(byte[] content, CancellationToken cancellationToken);
}

public interface IRightsRepository
{
    Task AddHolderAsync(Holder holder, CancellationToken cancellationToken);

    Task<Holder?> FindHolderAsync(Guid id, CancellationToken cancellationToken);

    Task<Holder?> FindHolderByEmailAsync(string email, CancellationToken cancellationToken);

    Task<Holder?> FindHolderByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Page<Holder>> PageHoldersAsync(string? query, PageRequest page, CancellationToken cancellationToken);

    Task LinkHolderAsync(string email, Guid userId, CancellationToken cancellationToken);

    Task AddRightAsync(GraveRight right, CancellationToken cancellationToken);

    Task<GraveRight?> FindRightAsync(Guid id, CancellationToken cancellationToken);

    Task<GraveRight?> FindActiveRightAsync(Guid graveSiteId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GraveRight>> ListRightsForHolderAsync(Guid holderId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, IReadOnlyList<BilledHolder>>> ActiveBillsAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task AddReservationAsync(Reservation reservation, CancellationToken cancellationToken);

    Task<Reservation?> FindReservationAsync(Guid id, CancellationToken cancellationToken);

    Task<Reservation?> FindActiveReservationAsync(Guid graveSiteId, CancellationToken cancellationToken);

    Task AddDocumentAsync(SiteDocument document, CancellationToken cancellationToken);

    Task<SiteDocument?> FindDocumentAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<SiteDocument>> ListDocumentsAsync(Guid graveSiteId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SiteDocument>> ListDocumentsForSitesAsync(IReadOnlyCollection<Guid> graveSiteIds, CancellationToken cancellationToken);
}

public interface ICemeteryReadModel
{
    Task<(string Etag, string Json)> MapAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task<IReadOnlyList<SearchHit>> SearchAsync(string query, Guid? cemeteryId, bool includeHolders, CancellationToken cancellationToken);

    Task AddEntranceAsync(Entrance entrance, CancellationToken cancellationToken);

    Task<Entrance?> FindEntranceAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Entrance>> ListEntrancesAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task RemoveEntranceAsync(Entrance entrance, CancellationToken cancellationToken);

    Task AddPathAsync(CemeteryPath path, CancellationToken cancellationToken);

    Task<CemeteryPath?> FindPathAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CemeteryPath>> ListPathsAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task RemovePathAsync(CemeteryPath path, CancellationToken cancellationToken);

    Task<WorkOrderPhoto?> LatestPhotoAsync(Guid graveSiteId, CancellationToken cancellationToken);

    Task<WorkOrderPhoto?> FindPhotoAsync(Guid workOrderId, Guid photoId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkOrder>> ListOpenWorkOrdersAsync(Guid graveSiteId, CancellationToken cancellationToken);
}

public sealed record SearchHit(string Kind, Guid Id, string Label, Guid? SiteId, double? Longitude, double? Latitude);

using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Operations;
using Cemetery.Domain.Register;
using Cemetery.Domain.Rights;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Application.Abstractions;

public interface IOrganizationRepository
{
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    Task AddAsync(Organization organization, CancellationToken cancellationToken);

    Task<Organization?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganizationMembership>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IMembershipRepository
{
    Task AddAsync(Membership membership, CancellationToken cancellationToken);

    Task<Membership?> FindForUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken);

    Task<MembershipRole?> RoleInCurrentTenantAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IInvitationRepository
{
    Task AddAsync(Invitation invitation, CancellationToken cancellationToken);

    Task<Invitation?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);
}

public interface ILayoutRepository
{
    Task AddCemeteryAsync(CemeteryPlace cemetery, CancellationToken cancellationToken);

    Task<CemeteryPlace?> FindCemeteryAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<CemeteryPlace>> ListCemeteriesAsync(CancellationToken cancellationToken);

    Task AddSectionAsync(Section section, CancellationToken cancellationToken);

    Task<Section?> FindSectionAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Section>> ListSectionsAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task<bool> SectionCodeExistsAsync(Guid cemeteryId, string code, CancellationToken cancellationToken);

    Task<bool> SectionHasClosedSiteAsync(Guid sectionId, CancellationToken cancellationToken);

    Task RemoveSectionAsync(Section section, CancellationToken cancellationToken);

    Task AddRowAsync(GraveRow row, CancellationToken cancellationToken);

    Task<int> CountRowsAsync(Guid sectionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GraveRow>> ListRowsAsync(Guid sectionId, CancellationToken cancellationToken);

    Task AddGraveSiteAsync(GraveSite site, CancellationToken cancellationToken);

    Task<GraveSite?> FindGraveSiteAsync(Guid id, CancellationToken cancellationToken);

    Task RemoveGraveSitesAsync(IReadOnlyList<GraveSite> sites, CancellationToken cancellationToken);

    Task<IReadOnlyList<GraveSite>> ListByCemeteryAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GraveSite>> ListOpenInSectionAsync(Guid sectionId, CancellationToken cancellationToken);

    Task<int> CountGraveSitesAsync(CancellationToken cancellationToken);

    Task<int> NextSequenceAsync(Guid cemeteryId, CancellationToken cancellationToken);
}

public interface IPublicCatalog
{
    Task<IReadOnlyList<Catalog.PublicCemeterySummary>> ListCemeteriesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Catalog.PublicSearchHit>> SearchAsync(string name, int? yearFrom, int? yearTo, string? slug, DateOnly today, CancellationToken cancellationToken);

    Task<Catalog.PublicMemorial?> FindMemorialAsync(Guid deceasedId, DateOnly today, CancellationToken cancellationToken);

    Task<Catalog.PublicMap?> FindMapAsync(string slug, Guid cemeteryId, CancellationToken cancellationToken);
}

public interface IRegisterRepository
{
    Task AddDeceasedAsync(Deceased deceased, CancellationToken cancellationToken);

    Task<Deceased?> FindDeceasedAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Deceased>> ListDeceasedAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task AddRevisionAsync(DeceasedRevision revision, CancellationToken cancellationToken);

    Task AddIntermentAsync(Interment interment, CancellationToken cancellationToken);

    Task<Interment?> FindIntermentAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Interment>> ListIntermentsBySiteAsync(Guid graveSiteId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Interment>> ListIntermentsByCemeteryAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Interment>> ListIntermentsByDeceasedAsync(Guid deceasedId, CancellationToken cancellationToken);

    Task AddAuditAsync(AuditEntry entry, CancellationToken cancellationToken);
}

public interface IOperationsRepository
{
    Task AddFuneralAsync(Funeral funeral, CancellationToken cancellationToken);

    Task<Funeral?> FindFuneralAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Funeral>> ListFuneralsAsync(Guid cemeteryId, CancellationToken cancellationToken);

    void RemoveFuneral(Funeral funeral);

    Task AddBookingAsync(ChapelBooking booking, CancellationToken cancellationToken);

    Task<ChapelBooking?> FindBookingAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChapelBooking>> ListBookingsAsync(Guid cemeteryId, CancellationToken cancellationToken);

    void RemoveBooking(ChapelBooking booking);

    Task AddWorkOrderAsync(WorkOrder order, CancellationToken cancellationToken);

    Task<WorkOrder?> FindWorkOrderAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkOrder>> ListWorkOrdersAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkOrder>> ListOpenOnAsync(DateOnly day, CancellationToken cancellationToken);

    Task RemoveWorkOrderAsync(WorkOrder order, CancellationToken cancellationToken);

    Task AddPermitAsync(MonumentPermit permit, CancellationToken cancellationToken);

    Task<MonumentPermit?> FindPermitAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<MonumentPermit>> ListPermitsAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task RemovePermitAsync(MonumentPermit permit, CancellationToken cancellationToken);

    Task AddPhotoAsync(WorkOrderPhoto photo, CancellationToken cancellationToken);
}

public interface IOfficeRequestRepository
{
    Task AddAsync(OfficeRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<OfficeRequest>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<OfficeRequest>> ListForAuthorAsync(Guid authorId, CancellationToken cancellationToken);
}

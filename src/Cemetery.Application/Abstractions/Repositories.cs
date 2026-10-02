using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
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

    Task AddRowAsync(GraveRow row, CancellationToken cancellationToken);

    Task<int> CountRowsAsync(Guid sectionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GraveRow>> ListRowsAsync(Guid sectionId, CancellationToken cancellationToken);

    Task AddGraveSiteAsync(GraveSite site, CancellationToken cancellationToken);

    Task<GraveSite?> FindGraveSiteAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<GraveSite>> ListByCemeteryAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GraveSite>> ListOpenInSectionAsync(Guid sectionId, CancellationToken cancellationToken);

    Task<int> CountGraveSitesAsync(CancellationToken cancellationToken);

    Task<int> NextSequenceAsync(Guid cemeteryId, CancellationToken cancellationToken);
}

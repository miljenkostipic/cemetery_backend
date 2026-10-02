using Cemetery.Domain.Identity;

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

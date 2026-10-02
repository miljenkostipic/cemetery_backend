using Cemetery.Application.Abstractions;
using Cemetery.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Infrastructure.Persistence;

internal sealed class OrganizationRepository(CemeteryDbContext db) : IOrganizationRepository
{
    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        db.Organizations.IgnoreQueryFilters().AnyAsync(entity => entity.Slug == slug, cancellationToken);

    public Task AddAsync(Organization organization, CancellationToken cancellationToken)
    {
        db.Organizations.Add(organization);
        return Task.CompletedTask;
    }

    public Task<Organization?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Organizations.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OrganizationMembership>> ListForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.Memberships.IgnoreQueryFilters()
            .Where(membership => membership.UserId == userId)
            .Join(
                db.Organizations.IgnoreQueryFilters(),
                membership => membership.TenantId,
                organization => organization.Id,
                (membership, organization) => new OrganizationMembership(
                    organization.Id,
                    organization.Name,
                    organization.Slug,
                    membership.Role))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

internal sealed class MembershipRepository(CemeteryDbContext db) : IMembershipRepository
{
    public Task AddAsync(Membership membership, CancellationToken cancellationToken)
    {
        db.Memberships.Add(membership);
        return Task.CompletedTask;
    }

    public Task<Membership?> FindForUserAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        db.Memberships.IgnoreQueryFilters().FirstOrDefaultAsync(
            membership => membership.TenantId == organizationId && membership.UserId == userId,
            cancellationToken);

    public async Task<MembershipRole?> RoleInCurrentTenantAsync(Guid userId, CancellationToken cancellationToken)
    {
        var membership = await db.Memberships
            .FirstOrDefaultAsync(entity => entity.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        return membership?.Role;
    }
}

internal sealed class InvitationRepository(CemeteryDbContext db) : IInvitationRepository
{
    public Task AddAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        db.Invitations.Add(invitation);
        return Task.CompletedTask;
    }

    public Task<Invitation?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Invitations.IgnoreQueryFilters().FirstOrDefaultAsync(invitation => invitation.TokenHash == tokenHash, cancellationToken);
}

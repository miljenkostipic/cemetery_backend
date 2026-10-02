using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;

namespace Cemetery.Application.Auth;

public sealed record GetSession;

public sealed record SessionView(
    Guid UserId,
    string Email,
    string DisplayName,
    Guid? OrganizationId,
    string? OrganizationName,
    string? Role);

public sealed class GetSessionHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IUserAccountGateway users,
    IOrganizationRepository organizations,
    IMembershipRepository memberships) : IQueryHandler<GetSession, SessionView>
{
    public async Task<SessionView> Handle(GetSession query, CancellationToken cancellationToken)
    {
        var account = await RequireAccountAsync(cancellationToken).ConfigureAwait(false);
        var active = await ActiveOrganizationAsync(account.Id, cancellationToken).ConfigureAwait(false);
        return new SessionView(
            account.Id,
            account.Email.Value,
            account.DisplayName,
            active?.OrganizationId,
            active?.Name,
            active is null ? null : MembershipRoles.ToCode(active.Role));
    }

    private async Task<UserAccount> RequireAccountAsync(CancellationToken cancellationToken)
    {
        var email = current.Email ?? throw new UnauthorizedException("auth.required");
        return await users.FindByEmailAsync(EmailAddress.Parse(email), cancellationToken).ConfigureAwait(false)
            ?? throw new UnauthorizedException("auth.required");
    }

    private async Task<OrganizationMembership?> ActiveOrganizationAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (tenant.OrganizationId is not Guid organizationId)
            return null;

        var organization = await organizations.FindAsync(organizationId, cancellationToken).ConfigureAwait(false);
        var role = await memberships.RoleInCurrentTenantAsync(userId, cancellationToken).ConfigureAwait(false);
        if (organization is null || role is null)
            return null;

        return new OrganizationMembership(organization.Id, organization.Name, organization.Slug, role.Value);
    }
}

using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;

namespace Cemetery.Application.Layout;

internal static class LayoutAccess
{
    public static async Task<Guid> RequireClerkAsync(
        ICurrentUser current,
        ITenantContext tenant,
        IMembershipRepository memberships,
        CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedException("auth.required");
        var organizationId = tenant.OrganizationId ?? throw new ForbiddenException("tenant.required");
        var role = await memberships.RoleInCurrentTenantAsync(userId, cancellationToken).ConfigureAwait(false);
        if (role is not MembershipRole.OrganizationAdmin and not MembershipRole.CemeteryClerk)
            throw new ForbiddenException("layout.forbidden");
        return organizationId;
    }
}

using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Register;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;

namespace Cemetery.Application.Operations;

internal static class OperationsAccess
{
    public static Task<Guid> RequireClerkAsync(
        ICurrentUser current,
        ITenantContext tenant,
        IMembershipRepository memberships,
        CancellationToken cancellationToken) =>
        RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken);

    public static async Task<Guid> RequireFieldAsync(
        ICurrentUser current,
        ITenantContext tenant,
        IMembershipRepository memberships,
        CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedException("auth.required");
        var organizationId = tenant.OrganizationId ?? throw new ForbiddenException("tenant.required");
        var role = await memberships.RoleInCurrentTenantAsync(userId, cancellationToken).ConfigureAwait(false);
        if (role is not MembershipRole.OrganizationAdmin and not MembershipRole.CemeteryClerk and not MembershipRole.FieldWorker)
            throw new ForbiddenException("register.forbidden");
        return organizationId;
    }
}

internal static class OperationsGate
{
    public static async Task RequireCemeteryAsync(ILayoutRepository layouts, Guid cemeteryId, CancellationToken cancellationToken)
    {
        _ = await layouts.FindCemeteryAsync(cemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
    }

    public static async Task<GraveSite> RequireSiteAsync(
        ILayoutRepository layouts,
        Guid cemeteryId,
        Guid graveSiteId,
        string code,
        CancellationToken cancellationToken)
    {
        var site = await layouts.FindGraveSiteAsync(graveSiteId, cancellationToken).ConfigureAwait(false);
        if (site is null || site.CemeteryId != cemeteryId)
            throw new DomainRuleException(code);
        return site;
    }

    public static async Task<Deceased> RequirePersonAsync(
        IRegisterRepository register,
        Guid cemeteryId,
        Guid deceasedId,
        CancellationToken cancellationToken)
    {
        var person = await register.FindDeceasedAsync(deceasedId, cancellationToken).ConfigureAwait(false);
        if (person is null || person.RemovedAt is not null || person.CemeteryId != cemeteryId)
            throw new DomainRuleException("funeral.deceased_invalid");
        return person;
    }
}

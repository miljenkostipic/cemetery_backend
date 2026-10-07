using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Rights;
using FluentValidation;

namespace Cemetery.Application.Rights;

public sealed record OfficeRequestView(Guid Id, string Message, DateOnly CreatedOn, Guid? GraveSiteId);

public sealed record ListOfficeRequests;

public sealed class ListOfficeRequestsHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IOfficeRequestRepository requests) : IQueryHandler<ListOfficeRequests, IReadOnlyList<OfficeRequestView>>
{
    public async Task<IReadOnlyList<OfficeRequestView>> Handle(ListOfficeRequests query, CancellationToken cancellationToken)
    {
        await OfficeAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var rows = await requests.ListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(OfficeMaps.View).ToArray();
    }
}

public sealed record ListMyOfficeRequests;

public sealed class ListMyOfficeRequestsHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IOfficeRequestRepository requests) : IQueryHandler<ListMyOfficeRequests, IReadOnlyList<OfficeRequestView>>
{
    public async Task<IReadOnlyList<OfficeRequestView>> Handle(ListMyOfficeRequests query, CancellationToken cancellationToken)
    {
        var userId = await OfficeAccess.RequireMemberAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var rows = await requests.ListForAuthorAsync(userId, cancellationToken).ConfigureAwait(false);
        return rows.Select(OfficeMaps.View).ToArray();
    }
}

public sealed record SendOfficeRequest(string Message, Guid? GraveSiteId);

public sealed class SendOfficeRequestValidator : AbstractValidator<SendOfficeRequest>
{
    public SendOfficeRequestValidator()
    {
        RuleFor(command => command.Message)
            .Must(text => (text ?? "").Trim().Length is >= 1 and <= OfficeRequest.MaxMessageLength)
            .WithErrorCode("request.message_invalid");
        RuleFor(command => command.GraveSiteId).Must(id => id != Guid.Empty).WithErrorCode("grave_site.not_found");
    }
}

public sealed class SendOfficeRequestHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IOfficeRequestRepository requests,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<SendOfficeRequest, OfficeRequestView>
{
    public async Task<OfficeRequestView> Handle(SendOfficeRequest command, CancellationToken cancellationToken)
    {
        var userId = await OfficeAccess.RequireMemberAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var organizationId = tenant.OrganizationId ?? throw new ForbiddenException("tenant.required");
        if (command.GraveSiteId is Guid graveSiteId)
        {
            var site = await layouts.FindGraveSiteAsync(graveSiteId, cancellationToken).ConfigureAwait(false);
            if (site is null)
                throw new NotFoundException("grave_site.not_found");
        }

        var created = OfficeRequest.Send(organizationId, userId, command.Message, command.GraveSiteId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        await requests.AddAsync(created, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OfficeMaps.View(created);
    }
}

internal static class OfficeMaps
{
    public static OfficeRequestView View(OfficeRequest request) =>
        new(request.Id, request.Message, request.CreatedOn, request.GraveSiteId);
}

internal static class OfficeAccess
{
    public static async Task<Guid> RequireMemberAsync(
        ICurrentUser current,
        ITenantContext tenant,
        IMembershipRepository memberships,
        CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new UnauthorizedException("auth.required");
        if (tenant.OrganizationId is null)
            throw new ForbiddenException("tenant.required");
        var role = await memberships.RoleInCurrentTenantAsync(userId, cancellationToken).ConfigureAwait(false);
        if (role is null)
            throw new ForbiddenException("tenant.forbidden");
        return userId;
    }

    public static async Task RequireClerkAsync(
        ICurrentUser current,
        ITenantContext tenant,
        IMembershipRepository memberships,
        CancellationToken cancellationToken)
    {
        var userId = await RequireMemberAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var role = await memberships.RoleInCurrentTenantAsync(userId, cancellationToken).ConfigureAwait(false);
        if (role is not MembershipRole.OrganizationAdmin and not MembershipRole.CemeteryClerk)
            throw new ForbiddenException("register.forbidden");
    }
}

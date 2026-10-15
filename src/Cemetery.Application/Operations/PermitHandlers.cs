using Cemetery.Application.Abstractions;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Operations;
using FluentValidation;

namespace Cemetery.Application.Operations;

public sealed record IssuePermit(Guid CemeteryId, Guid GraveSiteId, string HolderName, DateOnly IssuedOn, DateOnly ExpiresOn);

public sealed class IssuePermitValidator : AbstractValidator<IssuePermit>
{
    public IssuePermitValidator()
    {
        RuleFor(command => command.CemeteryId).NotEmpty().WithErrorCode("cemetery.not_found");
        RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("permit.site_invalid");
        RuleFor(command => command.HolderName).NotEmpty().WithErrorCode("permit.name_invalid");
        RuleFor(command => command.IssuedOn).Must(day => day != default).WithErrorCode("permit.date_invalid");
        RuleFor(command => command.ExpiresOn).Must(day => day != default).WithErrorCode("permit.date_invalid");
    }
}

public sealed class IssuePermitHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IOperationsRepository store,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<IssuePermit, MonumentPermitView>
{
    public async Task<MonumentPermitView> Handle(IssuePermit command, CancellationToken cancellationToken)
    {
        var organizationId = await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        await OperationsGate.RequireCemeteryAsync(layouts, command.CemeteryId, cancellationToken).ConfigureAwait(false);
        var site = await OperationsGate.RequireSiteAsync(layouts, command.CemeteryId, command.GraveSiteId, "permit.site_invalid", cancellationToken).ConfigureAwait(false);
        var permit = await Save(organizationId, command, cancellationToken).ConfigureAwait(false);
        return OperationsMaps.Permit(permit, site, OperationsClock.Today(clock));
    }

    private async Task<MonumentPermit> Save(Guid organizationId, IssuePermit command, CancellationToken cancellationToken)
    {
        var permit = MonumentPermit.Issue(organizationId, command.CemeteryId, command.GraveSiteId, command.HolderName, command.IssuedOn, command.ExpiresOn);
        await store.AddPermitAsync(permit, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return permit;
    }
}

public sealed record ListPermits(Guid CemeteryId);

public sealed class ListPermitsHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IOperationsRepository store,
    TimeProvider clock) : IQueryHandler<ListPermits, IReadOnlyList<MonumentPermitView>>
{
    public async Task<IReadOnlyList<MonumentPermitView>> Handle(ListPermits query, CancellationToken cancellationToken)
    {
        await OperationsAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var permits = await store.ListPermitsAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var sites = OperationsIndex.ById(await layouts.ListByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false), site => site.Id);
        var today = OperationsClock.Today(clock);
        return permits.Select(permit => Map(permit, sites, today)).ToArray();
    }

    private static MonumentPermitView Map(MonumentPermit permit, Dictionary<Guid, GraveSite> sites, DateOnly today)
    {
        sites.TryGetValue(permit.GraveSiteId, out var site);
        return OperationsMaps.Permit(permit, site, today);
    }
}

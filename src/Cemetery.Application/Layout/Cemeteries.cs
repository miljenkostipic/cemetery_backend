using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Layout;
using FluentValidation;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Application.Layout;

public sealed record OpenCemetery(string Name, bool Schematic);

public sealed class OpenCemeteryValidator : AbstractValidator<OpenCemetery>
{
    public OpenCemeteryValidator()
    {
        RuleFor(command => command.Name).Must(CemeteryPlace.IsValidName).WithErrorCode("cemetery.name_invalid");
    }
}

public sealed class OpenCemeteryHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<OpenCemetery, CemeteryView>
{
    public async Task<CemeteryView> Handle(OpenCemetery command, CancellationToken cancellationToken)
    {
        var organizationId = await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var cemetery = CemeteryPlace.Open(organizationId, command.Name, command.Schematic, clock.GetUtcNow());
        await layouts.AddCemeteryAsync(cemetery, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LayoutMaps.ToView(cemetery);
    }
}

public sealed record ListCemeteries;

public sealed class ListCemeteriesHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts) : IQueryHandler<ListCemeteries, IReadOnlyList<CemeteryView>>
{
    public async Task<IReadOnlyList<CemeteryView>> Handle(ListCemeteries query, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var cemeteries = await layouts.ListCemeteriesAsync(cancellationToken).ConfigureAwait(false);
        return cemeteries.Select(LayoutMaps.ToView).ToArray();
    }
}

public sealed record GetCemetery(Guid CemeteryId);

public sealed class GetCemeteryHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts) : IQueryHandler<GetCemetery, CemeteryView>
{
    public async Task<CemeteryView> Handle(GetCemetery query, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var cemetery = await layouts.FindCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        return LayoutMaps.ToView(cemetery);
    }
}

public sealed record SetCemeteryPlan(Guid CemeteryId, string PlanImageUrl, IReadOnlyList<GeoPointView> PlanBounds);

public sealed class SetCemeteryPlanValidator : AbstractValidator<SetCemeteryPlan>
{
    public SetCemeteryPlanValidator()
    {
        RuleFor(command => command.PlanImageUrl).NotEmpty().WithErrorCode("cemetery.plan_invalid");
        RuleFor(command => command.PlanBounds).Must(bounds => LayoutMaps.TryPolygon(bounds, out _)).WithErrorCode("geometry.ring_invalid");
    }
}

public sealed class SetCemeteryPlanHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork) : ICommandHandler<SetCemeteryPlan, CemeteryView>
{
    public async Task<CemeteryView> Handle(SetCemeteryPlan command, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var cemetery = await layouts.FindCemeteryAsync(command.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var bounds = LayoutMaps.Polygon(command.PlanBounds) ?? throw new ValidationFailedException(["geometry.ring_invalid"]);
        cemetery.SetPlan(command.PlanImageUrl, bounds);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LayoutMaps.ToView(cemetery);
    }
}

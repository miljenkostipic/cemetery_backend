using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Layout;
using FluentValidation;

namespace Cemetery.Application.Layout;

public sealed record ListGraveSites(Guid CemeteryId);

public sealed class ListGraveSitesHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts) : IQueryHandler<ListGraveSites, IReadOnlyList<GraveSiteView>>
{
    public async Task<IReadOnlyList<GraveSiteView>> Handle(ListGraveSites query, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var sites = await layouts.ListByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return sites.Select(LayoutMaps.ToView).ToArray();
    }
}

public sealed record GetGraveSite(Guid GraveSiteId);

public sealed class GetGraveSiteHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts) : IQueryHandler<GetGraveSite, GraveSiteView>
{
    public async Task<GraveSiteView> Handle(GetGraveSite query, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var site = await layouts.FindGraveSiteAsync(query.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        return LayoutMaps.ToView(site);
    }
}

public sealed record ReplaceGraveSiteOutline(Guid GraveSiteId, IReadOnlyList<GeoPointView> Outline);

public sealed class ReplaceGraveSiteOutlineValidator : AbstractValidator<ReplaceGraveSiteOutline>
{
    public ReplaceGraveSiteOutlineValidator()
    {
        RuleFor(command => command.Outline).Must(outline => LayoutMaps.TryPolygon(outline, out _)).WithErrorCode("geometry.ring_invalid");
    }
}

public sealed class ReplaceGraveSiteOutlineHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork) : ICommandHandler<ReplaceGraveSiteOutline, GraveSiteView>
{
    public async Task<GraveSiteView> Handle(ReplaceGraveSiteOutline command, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var site = await layouts.FindGraveSiteAsync(command.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var outline = LayoutMaps.Polygon(command.Outline) ?? throw new ValidationFailedException(["geometry.ring_invalid"]);
        var open = await layouts.ListOpenInSectionAsync(site.SectionId, cancellationToken).ConfigureAwait(false);
        GraveSite.RejectOverlap(outline, open, site.Id);
        site.ReplaceOutline(outline);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LayoutMaps.ToView(site);
    }
}

public sealed record SplitGraveSite(Guid GraveSiteId);

public sealed class SplitGraveSiteHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork) : ICommandHandler<SplitGraveSite, IReadOnlyList<GraveSiteView>>
{
    public async Task<IReadOnlyList<GraveSiteView>> Handle(SplitGraveSite command, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var site = await layouts.FindGraveSiteAsync(command.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var sequence = await layouts.NextSequenceAsync(site.CemeteryId, cancellationToken).ConfigureAwait(false);
        var (left, right) = site.Split($"{site.Code}-L", $"{site.Code}-R{sequence:0000}");
        await layouts.AddGraveSiteAsync(left, cancellationToken).ConfigureAwait(false);
        await layouts.AddGraveSiteAsync(right, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return [LayoutMaps.ToView(left), LayoutMaps.ToView(right)];
    }
}

public sealed record MergeGraveSites(Guid LeftId, Guid RightId);

public sealed class MergeGraveSitesHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork) : ICommandHandler<MergeGraveSites, GraveSiteView>
{
    public async Task<GraveSiteView> Handle(MergeGraveSites command, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var left = await layouts.FindGraveSiteAsync(command.LeftId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var right = await layouts.FindGraveSiteAsync(command.RightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var sequence = await layouts.NextSequenceAsync(left.CemeteryId, cancellationToken).ConfigureAwait(false);
        var section = await layouts.FindSectionAsync(left.SectionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("section.not_found");
        var merged = GraveSite.Merge(left, right, $"{section.Code}-{sequence:0000}");
        await layouts.AddGraveSiteAsync(merged, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LayoutMaps.ToView(merged);
    }
}

public sealed record CloseGraveSite(Guid GraveSiteId);

public sealed class CloseGraveSiteHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork) : ICommandHandler<CloseGraveSite, GraveSiteView>
{
    public async Task<GraveSiteView> Handle(CloseGraveSite command, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var site = await layouts.FindGraveSiteAsync(command.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        site.Close();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LayoutMaps.ToView(site);
    }
}

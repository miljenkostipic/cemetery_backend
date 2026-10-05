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

public sealed class SplitGraveSiteValidator : AbstractValidator<SplitGraveSite>
{
    public SplitGraveSiteValidator()
    {
        RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("grave_site.not_found");
    }
}

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

public sealed class MergeGraveSitesValidator : AbstractValidator<MergeGraveSites>
{
    public MergeGraveSitesValidator()
    {
        RuleFor(command => command.LeftId).NotEmpty().WithErrorCode("grave_site.not_found");
        RuleFor(command => command.RightId).NotEmpty().WithErrorCode("grave_site.not_found");
        RuleFor(command => command.RightId).NotEqual(command => command.LeftId).WithErrorCode("grave_site.merge_invalid");
    }
}

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

public sealed class CloseGraveSiteValidator : AbstractValidator<CloseGraveSite>
{
    public CloseGraveSiteValidator()
    {
        RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("grave_site.not_found");
    }
}

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

public sealed record ReopenGraveSite(Guid GraveSiteId);

public sealed class ReopenGraveSiteValidator : AbstractValidator<ReopenGraveSite>
{
    public ReopenGraveSiteValidator()
    {
        RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("grave_site.not_found");
    }
}

public sealed class ReopenGraveSiteHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork) : ICommandHandler<ReopenGraveSite, GraveSiteView>
{
    public async Task<GraveSiteView> Handle(ReopenGraveSite command, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var site = await layouts.FindGraveSiteAsync(command.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var others = await layouts.ListByCemeteryAsync(site.CemeteryId, cancellationToken).ConfigureAwait(false);
        var covered = PiecesCovering(site, others);
        if (covered is null)
            throw new ConflictException("grave_site.reset_invalid");
        if (covered.Length > 0)
            await layouts.RemoveGraveSitesAsync(covered, cancellationToken).ConfigureAwait(false);
        site.Reopen();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LayoutMaps.ToView(site);
    }

    private static GraveSite[]? PiecesCovering(GraveSite site, IReadOnlyList<GraveSite> cemetery)
    {
        var others = cemetery.Where(other => other.Id != site.Id && other.SectionId == site.SectionId).ToArray();
        var descendants = others.Where(other => IsSplitChild(site.Code, other.Code)).ToArray();
        if (descendants.Length > 0)
            return descendants;
        if (site.Outline is null)
            return [];

        var overlapping = others.Where(other => other is { Closed: false, Outline: not null } && site.Outline.InteriorOverlaps(other.Outline)).ToArray();
        if (overlapping.Length == 0 || overlapping.All(other => Inside(site.Outline, other.Outline!)))
            return overlapping;
        return null;
    }

    private static bool IsSplitChild(string parentCode, string code)
    {
        var prefix = parentCode + "-";
        if (!code.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        foreach (var part in code[prefix.Length..].Split('-'))
        {
            if (part == "L")
                continue;
            if (part.Length < 5 || part[0] != 'R' || !part[1..].All(char.IsAsciiDigit))
                return false;
        }

        return true;
    }

    private static bool Inside(GeoPolygon outline, GeoPolygon piece)
    {
        const double tolerance = 1e-7;
        var minLon = outline.Ring.Min(point => point.Longitude);
        var maxLon = outline.Ring.Max(point => point.Longitude);
        var minLat = outline.Ring.Min(point => point.Latitude);
        var maxLat = outline.Ring.Max(point => point.Latitude);
        return piece.Ring.All(point =>
            point.Longitude >= minLon - tolerance && point.Longitude <= maxLon + tolerance
            && point.Latitude >= minLat - tolerance && point.Latitude <= maxLat + tolerance);
    }
}

public sealed record UndoGraveSiteSplit(Guid OriginalId, Guid LeftId, Guid RightId);

public sealed class UndoGraveSiteSplitValidator : AbstractValidator<UndoGraveSiteSplit>
{
    public UndoGraveSiteSplitValidator()
    {
        RuleFor(command => command.OriginalId).NotEmpty().WithErrorCode("grave_site.not_found");
        RuleFor(command => command.LeftId).NotEmpty().WithErrorCode("grave_site.reset_invalid");
        RuleFor(command => command.RightId).NotEmpty().NotEqual(command => command.LeftId).WithErrorCode("grave_site.reset_invalid");
    }
}

public sealed class UndoGraveSiteSplitHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork) : ICommandHandler<UndoGraveSiteSplit, GraveSiteView>
{
    public async Task<GraveSiteView> Handle(UndoGraveSiteSplit command, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var original = await layouts.FindGraveSiteAsync(command.OriginalId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var left = await layouts.FindGraveSiteAsync(command.LeftId, cancellationToken).ConfigureAwait(false)
            ?? throw new ConflictException("grave_site.reset_invalid");
        var right = await layouts.FindGraveSiteAsync(command.RightId, cancellationToken).ConfigureAwait(false)
            ?? throw new ConflictException("grave_site.reset_invalid");
        if (!original.Closed || left.Closed || right.Closed || left.SectionId != original.SectionId || right.SectionId != original.SectionId)
            throw new ConflictException("grave_site.reset_invalid");
        await layouts.RemoveGraveSitesAsync([left, right], cancellationToken).ConfigureAwait(false);
        original.Reopen();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LayoutMaps.ToView(original);
    }
}

using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Layout;
using FluentValidation;

namespace Cemetery.Application.Layout;

/// <summary>
/// One grave of any shape drawn on the map and placed in a section, optionally in a row. Without a
/// code it takes the next field-row-place code in its row, or the next field-number code among the
/// section's row-less graves.
/// </summary>
public sealed record DrawGraveSite(
    Guid SectionId,
    IReadOnlyList<GeoPointView> Outline,
    string Kind,
    int? Capacity,
    string? Code,
    Guid? RowId);

public sealed class DrawGraveSiteValidator : AbstractValidator<DrawGraveSite>
{
    public DrawGraveSiteValidator()
    {
        RuleFor(command => command.Outline).Must(outline => LayoutMaps.TryPolygon(outline, out _)).WithErrorCode("geometry.ring_invalid");
        RuleFor(command => command.Outline).Must(LayoutMaps.WithinPointLimit).WithErrorCode("grave_site.outline_too_complex");
        RuleFor(command => command.Kind).Must(kind => GraveSiteKinds.TryParse(kind, out _)).WithErrorCode("grave_site.kind_invalid");
        RuleFor(command => command.Code)
            .Must(code => code is null || (!string.IsNullOrWhiteSpace(code) && code.Trim().Length <= GraveSite.CodeMaxLength))
            .WithErrorCode("grave_site.code_invalid");
        RuleFor(command => command.Capacity).GreaterThanOrEqualTo(0).When(command => command.Capacity is not null).WithErrorCode("grave_site.capacity_invalid");
    }
}

public sealed class DrawGraveSiteHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork) : ICommandHandler<DrawGraveSite, GraveSiteView>
{
    public async Task<GraveSiteView> Handle(DrawGraveSite command, CancellationToken cancellationToken)
    {
        var organizationId = caller.OrganizationId;
        var section = await layouts.FindSectionAsync(command.SectionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("section.not_found");
        if (!GraveSiteKinds.TryParse(command.Kind, out var kind))
            throw new ValidationFailedException(["grave_site.kind_invalid"]);
        var outline = LayoutMaps.Polygon(command.Outline) ?? throw new ValidationFailedException(["geometry.ring_invalid"]);
        GraveSite.RejectDrawnShape(outline);
        section.RejectOutside(outline);
        var row = await RowAsync(section, command.RowId, cancellationToken).ConfigureAwait(false);
        var open = await layouts.ListOpenInSectionAsync(section.Id, cancellationToken).ConfigureAwait(false);
        GraveSite.RejectOverlap(outline, open);

        var code = await CodeAsync(section, row, command.Code, cancellationToken).ConfigureAwait(false);
        var site = GraveSite.Place(organizationId, section.CemeteryId, section.Id, row?.Id, code, kind, outline, command.Capacity);
        await layouts.AddGraveSiteAsync(site, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LayoutMaps.ToView(site);
    }

    private async Task<GraveRow?> RowAsync(Section section, Guid? rowId, CancellationToken cancellationToken)
    {
        if (rowId is null)
            return null;
        var rows = await layouts.ListRowsAsync(section.Id, cancellationToken).ConfigureAwait(false);
        return rows.FirstOrDefault(row => row.Id == rowId) ?? throw new NotFoundException("grave_row.not_found");
    }

    private async Task<string> CodeAsync(Section section, GraveRow? row, string? requested, CancellationToken cancellationToken)
    {
        var taken = (await layouts.ListGraveSiteCodesAsync(section.CemeteryId, cancellationToken).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);
        if (requested is not null)
        {
            var code = requested.Trim();
            return taken.Contains(code) ? throw new ConflictException("grave_site.code_taken") : code;
        }

        var siblings = await layouts.ListRowCodesAsync(section.Id, row?.Id, cancellationToken).ConfigureAwait(false);
        return row is null
            ? GraveSiteCodes.NextLoose(section.Code, siblings, taken)
            : GraveSiteCodes.NextInRow(section.Code, row.Label, siblings, taken);
    }
}

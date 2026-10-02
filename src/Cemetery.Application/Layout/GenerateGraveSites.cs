using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Layout;
using FluentValidation;

namespace Cemetery.Application.Layout;

public sealed record GenerateGraveSites(
    Guid SectionId,
    string Kind,
    int Rows,
    int Columns,
    double OriginLongitude,
    double OriginLatitude,
    double WidthMeters,
    double DepthMeters,
    double GapMeters,
    double RotationDegrees);

public sealed class GenerateGraveSitesValidator : AbstractValidator<GenerateGraveSites>
{
    public GenerateGraveSitesValidator()
    {
        RuleFor(command => command.Kind).Must(kind => GraveSiteKinds.TryParse(kind, out _)).WithErrorCode("grave_site.kind_invalid");
        RuleFor(command => command.Rows).InclusiveBetween(1, 100).WithErrorCode("grave_site.grid_invalid");
        RuleFor(command => command.Columns).InclusiveBetween(1, 100).WithErrorCode("grave_site.grid_invalid");
        RuleFor(command => command.WidthMeters).InclusiveBetween(0.3, 20).WithErrorCode("grave_site.grid_invalid");
        RuleFor(command => command.DepthMeters).InclusiveBetween(0.3, 20).WithErrorCode("grave_site.grid_invalid");
        RuleFor(command => command.GapMeters).InclusiveBetween(0, 5).WithErrorCode("grave_site.grid_invalid");
        RuleFor(command => command.RotationDegrees).InclusiveBetween(-360, 360).WithErrorCode("grave_site.grid_invalid");
        RuleFor(command => command.OriginLongitude).InclusiveBetween(-180, 180).WithErrorCode("geometry.coordinate_invalid");
        RuleFor(command => command.OriginLatitude).InclusiveBetween(-90, 90).WithErrorCode("geometry.coordinate_invalid");
    }
}

public sealed class GenerateGraveSitesHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork) : ICommandHandler<GenerateGraveSites, IReadOnlyList<GraveSiteView>>
{
    public async Task<IReadOnlyList<GraveSiteView>> Handle(GenerateGraveSites command, CancellationToken cancellationToken)
    {
        var organizationId = await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var section = await layouts.FindSectionAsync(command.SectionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("section.not_found");
        if (!GraveSiteKinds.TryParse(command.Kind, out var kind))
            throw new ValidationFailedException(["grave_site.kind_invalid"]);

        var cells = GraveSiteGrid.Generate(
            new GeoPoint(command.OriginLongitude, command.OriginLatitude),
            command.Rows,
            command.Columns,
            command.WidthMeters,
            command.DepthMeters,
            command.GapMeters,
            command.RotationDegrees);
        var existing = await layouts.ListOpenInSectionAsync(section.Id, cancellationToken).ConfigureAwait(false);
        foreach (var cell in cells)
            GraveSite.RejectOverlap(cell, existing);

        var sequence = await layouts.NextSequenceAsync(section.CemeteryId, cancellationToken).ConfigureAwait(false);
        var rowCount = await layouts.CountRowsAsync(section.Id, cancellationToken).ConfigureAwait(false);
        var created = await PlaceRowsAsync(organizationId, section, kind, cells, sequence, rowCount, command.Columns, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return created.Select(LayoutMaps.ToView).ToArray();
    }

    private async Task<List<GraveSite>> PlaceRowsAsync(
        Guid organizationId,
        Section section,
        GraveSiteKind kind,
        IReadOnlyList<GeoPolygon> cells,
        int sequence,
        int rowCount,
        int columns,
        CancellationToken cancellationToken)
    {
        var created = new List<GraveSite>(cells.Count);
        Guid? rowId = null;
        for (var index = 0; index < cells.Count; index++)
        {
            if (index % columns == 0)
                rowId = await AddRowAsync(organizationId, section.Id, rowCount + index / columns + 1, cancellationToken).ConfigureAwait(false);
            var site = GraveSite.Place(organizationId, section.CemeteryId, section.Id, rowId, $"{section.Code}-{sequence + index:0000}", kind, cells[index]);
            await layouts.AddGraveSiteAsync(site, cancellationToken).ConfigureAwait(false);
            created.Add(site);
        }

        return created;
    }

    private async Task<Guid> AddRowAsync(Guid organizationId, Guid sectionId, int label, CancellationToken cancellationToken)
    {
        var row = GraveRow.Add(organizationId, sectionId, label.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await layouts.AddRowAsync(row, cancellationToken).ConfigureAwait(false);
        return row.Id;
    }
}

using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Layout;
using FluentValidation;

namespace Cemetery.Application.Layout;

public sealed record AddSection(Guid CemeteryId, string Name, string Code, IReadOnlyList<GeoPointView>? Outline);

public sealed class AddSectionValidator : AbstractValidator<AddSection>
{
    public AddSectionValidator()
    {
        RuleFor(command => command.Name).Must(Section.IsValidName).WithErrorCode("section.name_invalid");
        RuleFor(command => command.Code).Must(Section.IsValidCode).WithErrorCode("section.code_invalid");
        RuleFor(command => command.Outline).Must(outline => outline is null || outline.Count == 0 || LayoutMaps.TryPolygon(outline, out _)).WithErrorCode("geometry.ring_invalid");
    }
}

public sealed class AddSectionHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork) : ICommandHandler<AddSection, SectionView>
{
    public async Task<SectionView> Handle(AddSection command, CancellationToken cancellationToken)
    {
        var organizationId = await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        _ = await layouts.FindCemeteryAsync(command.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("cemetery.not_found");
        var code = command.Code.Trim().ToUpperInvariant();
        if (await layouts.SectionCodeExistsAsync(command.CemeteryId, code, cancellationToken).ConfigureAwait(false))
            throw new ConflictException("section.code_taken");

        var section = Section.Add(organizationId, command.CemeteryId, command.Name, code, LayoutMaps.Polygon(command.Outline));
        await layouts.AddSectionAsync(section, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LayoutMaps.ToView(section);
    }
}

public sealed record ListSections(Guid CemeteryId);

public sealed class ListSectionsHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts) : IQueryHandler<ListSections, IReadOnlyList<SectionView>>
{
    public async Task<IReadOnlyList<SectionView>> Handle(ListSections query, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var sections = await layouts.ListSectionsAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return sections.Select(LayoutMaps.ToView).ToArray();
    }
}

public sealed record ReplaceSectionOutline(Guid SectionId, IReadOnlyList<GeoPointView> Outline);

public sealed class ReplaceSectionOutlineValidator : AbstractValidator<ReplaceSectionOutline>
{
    public ReplaceSectionOutlineValidator()
    {
        RuleFor(command => command.Outline).Must(outline => LayoutMaps.TryPolygon(outline, out _)).WithErrorCode("geometry.ring_invalid");
    }
}

public sealed class ReplaceSectionOutlineHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork) : ICommandHandler<ReplaceSectionOutline, SectionView>
{
    public async Task<SectionView> Handle(ReplaceSectionOutline command, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var section = await layouts.FindSectionAsync(command.SectionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("section.not_found");
        var outline = LayoutMaps.Polygon(command.Outline) ?? throw new ValidationFailedException(["geometry.ring_invalid"]);
        section.ReplaceOutline(outline);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LayoutMaps.ToView(section);
    }
}

public sealed record AddGraveRow(Guid SectionId, string Label);

public sealed class AddGraveRowValidator : AbstractValidator<AddGraveRow>
{
    public AddGraveRowValidator()
    {
        RuleFor(command => command.Label).Must(GraveRow.IsValidLabel).WithErrorCode("row.label_invalid");
    }
}

public sealed class AddGraveRowHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IUnitOfWork unitOfWork) : ICommandHandler<AddGraveRow, GraveRowView>
{
    public async Task<GraveRowView> Handle(AddGraveRow command, CancellationToken cancellationToken)
    {
        var organizationId = await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        _ = await layouts.FindSectionAsync(command.SectionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("section.not_found");
        var row = GraveRow.Add(organizationId, command.SectionId, command.Label);
        await layouts.AddRowAsync(row, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return LayoutMaps.ToView(row);
    }
}

public sealed record ListGraveRows(Guid SectionId);

public sealed class ListGraveRowsHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts) : IQueryHandler<ListGraveRows, IReadOnlyList<GraveRowView>>
{
    public async Task<IReadOnlyList<GraveRowView>> Handle(ListGraveRows query, CancellationToken cancellationToken)
    {
        await LayoutAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var rows = await layouts.ListRowsAsync(query.SectionId, cancellationToken).ConfigureAwait(false);
        return rows.Select(LayoutMaps.ToView).ToArray();
    }
}

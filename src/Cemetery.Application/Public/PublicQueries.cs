using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Register;
using Cemetery.Domain.Register;

namespace Cemetery.Application.Catalog;

public sealed record SearchPublic(string Name, int? YearFrom, int? YearTo, string? OrganizationSlug);

public sealed class SearchPublicHandler(IPublicCatalog catalog, TimeProvider clock) : IQueryHandler<SearchPublic, IReadOnlyList<PublicSearchHit>>
{
    public Task<IReadOnlyList<PublicSearchHit>> Handle(SearchPublic query, CancellationToken cancellationToken)
    {
        var name = PublicListing.RequireSearchName(query.Name);
        var (from, to) = PublicListing.RequireYears(query.YearFrom, query.YearTo);
        var slug = string.IsNullOrWhiteSpace(query.OrganizationSlug) ? null : query.OrganizationSlug.Trim();
        return catalog.SearchAsync(name, from, to, slug, RegisterClock.Today(clock), cancellationToken);
    }
}

public sealed record GetMemorial(Guid DeceasedId);

public sealed class GetMemorialHandler(IPublicCatalog catalog, TimeProvider clock) : IQueryHandler<GetMemorial, PublicMemorial>
{
    public async Task<PublicMemorial> Handle(GetMemorial query, CancellationToken cancellationToken)
    {
        var found = await catalog.FindMemorialAsync(query.DeceasedId, RegisterClock.Today(clock), cancellationToken).ConfigureAwait(false);
        return found ?? throw new NotFoundException("memorial.not_found");
    }
}

public sealed record GetPublicMap(string Slug, Guid CemeteryId);

public sealed class GetPublicMapHandler(IPublicCatalog catalog) : IQueryHandler<GetPublicMap, PublicMap>
{
    public async Task<PublicMap> Handle(GetPublicMap query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Slug) || query.CemeteryId == Guid.Empty)
            throw new NotFoundException("cemetery.not_found");

        var found = await catalog.FindMapAsync(query.Slug.Trim(), query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return found ?? throw new NotFoundException("cemetery.not_found");
    }
}

public sealed record ListPublicCemeteries();

public sealed class ListPublicCemeteriesHandler(IPublicCatalog catalog) : IQueryHandler<ListPublicCemeteries, IReadOnlyList<PublicCemeterySummary>>
{
    public Task<IReadOnlyList<PublicCemeterySummary>> Handle(ListPublicCemeteries query, CancellationToken cancellationToken) =>
        catalog.ListCemeteriesAsync(cancellationToken);
}

public sealed record GetVisibility();

public sealed class GetVisibilityHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IOrganizationRepository organizations) : IQueryHandler<GetVisibility, VisibilityView>
{
    public async Task<VisibilityView> Handle(GetVisibility query, CancellationToken cancellationToken)
    {
        var organizationId = await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var organization = await organizations.FindAsync(organizationId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("organization.not_found");
        return new VisibilityView(organization.PublishesRegister, organization.HideRecentDeathsDays);
    }
}

using Cemetery.Application.Layout;

namespace Cemetery.Application.Catalog;

public sealed record PublicCemeterySummary(
    string OrganizationSlug,
    string OrganizationName,
    Guid CemeteryId,
    string CemeteryName,
    bool Schematic);

public sealed record PublicSearchHit(
    Guid DeceasedId,
    string GivenName,
    string FamilyName,
    DateOnly? BornOn,
    DateOnly? DiedOn,
    string OrganizationSlug,
    string OrganizationName,
    Guid CemeteryId,
    string CemeteryName,
    Guid? GraveSiteId,
    string? GraveSiteCode);

public sealed record PublicMemorial(
    Guid DeceasedId,
    string GivenName,
    string FamilyName,
    DateOnly? BornOn,
    DateOnly? DiedOn,
    string? Epitaph,
    string? PhotoUrl,
    string OrganizationSlug,
    string OrganizationName,
    Guid CemeteryId,
    string CemeteryName,
    bool Schematic,
    Guid? GraveSiteId,
    string? GraveSiteCode,
    IReadOnlyList<GeoPointView> Outline);

public sealed record PublicSection(Guid Id, string Name, string Code, IReadOnlyList<GeoPointView> Outline);

public sealed record PublicSite(Guid Id, string Code, IReadOnlyList<GeoPointView> Outline);

public sealed record PublicMap(
    string OrganizationSlug,
    string OrganizationName,
    Guid CemeteryId,
    string CemeteryName,
    bool Schematic,
    string? PlanImageUrl,
    IReadOnlyList<GeoPointView> PlanBounds,
    IReadOnlyList<PublicSection> Sections,
    IReadOnlyList<PublicSite> Sites);

public sealed record VisibilityView(bool PublishesRegister, int HideRecentDeathsDays);

public sealed record VisibilityBody(int HideRecentDeathsDays);

public sealed record MemorialBody(string? Epitaph, string? PhotoUrl);

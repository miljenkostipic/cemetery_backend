using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Application.Layout;

public sealed record GeoPointView(double Longitude, double Latitude);

public sealed record CemeteryView(
    Guid Id,
    string Name,
    bool Schematic,
    string? PlanImageUrl,
    IReadOnlyList<GeoPointView> PlanBounds,
    int RestPeriodYears);

public sealed record SectionView(Guid Id, Guid CemeteryId, string Name, string Code, IReadOnlyList<GeoPointView> Outline);

public sealed record GraveRowView(Guid Id, Guid SectionId, string Label);

public sealed record GraveSiteView(
    Guid Id,
    Guid CemeteryId,
    Guid SectionId,
    Guid? RowId,
    string Code,
    string Kind,
    int Capacity,
    string Status,
    IReadOnlyList<GeoPointView> Outline);

internal static class LayoutMaps
{
    public static CemeteryView ToView(CemeteryPlace cemetery) =>
        new(cemetery.Id, cemetery.Name, cemetery.Schematic, cemetery.PlanImageUrl, Ring(cemetery.PlanBounds), cemetery.RestPeriodYears);

    public static SectionView ToView(Section section) =>
        new(section.Id, section.CemeteryId, section.Name, section.Code, Ring(section.Outline));

    public static GraveRowView ToView(GraveRow row) => new(row.Id, row.SectionId, row.Label);

    public static GraveSiteView ToView(GraveSite site) => ToView(site, new SiteUse(0, false));

    public static GraveSiteView ToView(GraveSite site, SiteUse use) =>
        new(
            site.Id,
            site.CemeteryId,
            site.SectionId,
            site.RowId,
            site.Code,
            GraveSiteKinds.ToCode(site.Kind),
            site.Capacity,
            GraveSiteStatuses.ToCode(site.StatusFor(use)),
            Ring(site.Outline));

    public static GeoPolygon? Polygon(IReadOnlyList<GeoPointView>? ring) =>
        TryPolygon(ring, out var polygon) ? polygon : null;

    public static bool TryPolygon(IReadOnlyList<GeoPointView>? ring, out GeoPolygon? polygon)
    {
        polygon = null;
        if (ring is null || ring.Count == 0)
            return false;
        try
        {
            polygon = GeoPolygon.Create(ring.Select(point => new GeoPoint(point.Longitude, point.Latitude)).ToArray());
            return true;
        }
        catch (DomainRuleException)
        {
            return false;
        }
    }

    private static GeoPointView[] Ring(GeoPolygon? polygon) =>
        polygon is null ? [] : polygon.Ring.Select(point => new GeoPointView(point.Longitude, point.Latitude)).ToArray();
}

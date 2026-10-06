using Cemetery.Domain.Identity;
using Cemetery.Domain.Register;

namespace Cemetery.Domain.Layout;

public sealed class GraveSite : TenantEntity
{
    public const int CodeMaxLength = 32;

    private GraveSite()
    {
        Code = "";
    }

    public Guid CemeteryId { get; private set; }

    public Guid SectionId { get; private set; }

    public Guid? RowId { get; private set; }

    public string Code { get; private set; }

    public GraveSiteKind Kind { get; private set; }

    public int Capacity { get; private set; }

    public GeoPolygon? Outline { get; private set; }

    public bool Closed { get; private set; }

    public GraveSiteStatus Status => StatusFor(new SiteUse(0, false));

    public GraveSiteStatus StatusFor(SiteUse use) =>
        GraveSiteStatuses.Derive(Closed, Outline is not null, false, use.Occupied, Capacity, use.Reusable);

    public static GraveSite Place(
        Guid tenantId,
        Guid cemeteryId,
        Guid sectionId,
        Guid? rowId,
        string code,
        GraveSiteKind kind,
        GeoPolygon outline,
        int? capacity = null)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");
        if (cemeteryId == Guid.Empty || sectionId == Guid.Empty)
            throw new DomainRuleException("grave_site.location_required");
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > CodeMaxLength)
            throw new DomainRuleException("grave_site.code_invalid");

        var resolved = capacity ?? GraveSiteKinds.DefaultCapacity(kind);
        if (resolved < 0 || (kind == GraveSiteKind.Memorial && resolved != 0) || (kind != GraveSiteKind.Memorial && resolved < 1))
            throw new DomainRuleException("grave_site.capacity_invalid");

        return new GraveSite
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = cemeteryId,
            SectionId = sectionId,
            RowId = rowId,
            Code = code.Trim(),
            Kind = kind,
            Capacity = resolved,
            Outline = outline,
        };
    }

    public void ReplaceOutline(GeoPolygon outline)
    {
        if (Closed)
            throw new DomainRuleException("grave_site.closed");
        Outline = outline;
    }

    public void Close() => Closed = true;

    public void Reopen()
    {
        if (!Closed)
            throw new DomainRuleException("grave_site.reset_invalid");
        Closed = false;
    }

    public (GraveSite Left, GraveSite Right) Split(string leftCode, string rightCode)
    {
        if (Closed || Outline is null)
            throw new DomainRuleException("grave_site.split_invalid");

        var (leftOutline, rightOutline) = GeoPolygon.Split(Outline);
        Close();
        var left = Copy(leftCode, leftOutline);
        var right = Copy(rightCode, rightOutline);
        return (left, right);
    }

    public static GraveSite Merge(GraveSite left, GraveSite right, string code)
    {
        if (left.TenantId != right.TenantId || left.SectionId != right.SectionId || left.Kind != right.Kind)
            throw new DomainRuleException("grave_site.merge_invalid");
        if (left.Closed || right.Closed || left.Outline is null || right.Outline is null)
            throw new DomainRuleException("grave_site.merge_invalid");
        if (left.Id == right.Id)
            throw new DomainRuleException("grave_site.merge_invalid");

        var capacity = left.Kind == GraveSiteKind.Memorial ? 0 : left.Capacity + right.Capacity;
        var merged = Place(left.TenantId, left.CemeteryId, left.SectionId, left.RowId, code, left.Kind, GeoPolygon.BoundsOf(left.Outline, right.Outline), capacity);
        left.Close();
        right.Close();
        return merged;
    }

    public static void RejectOverlap(GeoPolygon candidate, IEnumerable<GraveSite> openSites, Guid? ignoreId = null)
    {
        foreach (var site in openSites)
        {
            if (site.Id == ignoreId || site.Closed || site.Outline is null)
                continue;
            if (candidate.InteriorOverlaps(site.Outline))
                throw new DomainRuleException("grave_site.overlaps");
        }
    }

    private GraveSite Copy(string code, GeoPolygon outline) =>
        Place(TenantId, CemeteryId, SectionId, RowId, code, Kind, outline, Capacity);
}

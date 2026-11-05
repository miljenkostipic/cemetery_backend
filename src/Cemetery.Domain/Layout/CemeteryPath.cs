using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Layout;

public sealed class GeoLine
{
    public const int MinimumPoints = 2;

    private GeoLine(IReadOnlyList<GeoPoint> points) => Points = points;

    public IReadOnlyList<GeoPoint> Points { get; }

    public static GeoLine Create(IReadOnlyList<GeoPoint> points)
    {
        if (points.Count < MinimumPoints)
            throw new DomainRuleException("geometry.line_invalid");

        foreach (var point in points)
            Require(point);

        return new GeoLine(points);
    }

    private static void Require(GeoPoint point)
    {
        if (point.Longitude is < -180 or > 180 || point.Latitude is < -90 or > 90)
            throw new DomainRuleException("geometry.coordinate_invalid");
    }
}

public sealed class Entrance : TenantEntity, IStampable
{
    public const int NameMaxLength = 80;

    private Entrance()
    {
        Name = "";
    }

    public Guid CemeteryId { get; private set; }

    public string Name { get; private set; }

    public GeoPoint Location { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Entrance Place(Guid tenantId, Guid cemeteryId, string? name, GeoPoint location)
    {
        if (tenantId == Guid.Empty || cemeteryId == Guid.Empty)
            throw new DomainRuleException("entrance.invalid");

        _ = GeoLine.Create([location, location]);
        return new Entrance
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = cemeteryId,
            Name = LayoutName.Require(name, NameMaxLength, "entrance.name_invalid"),
            Location = location,
        };
    }

    public void Stamp(DateTimeOffset at) => UpdatedAt = at;
}

public sealed class CemeteryPath : TenantEntity, IStampable
{
    public const int NameMaxLength = 80;

    private CemeteryPath()
    {
        Name = "";
        Line = GeoLine.Create([new GeoPoint(0, 0), new GeoPoint(0, 0)]);
    }

    public Guid CemeteryId { get; private set; }

    public string Name { get; private set; }

    public GeoLine Line { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static CemeteryPath Draw(Guid tenantId, Guid cemeteryId, string? name, GeoLine line)
    {
        if (tenantId == Guid.Empty || cemeteryId == Guid.Empty)
            throw new DomainRuleException("path.invalid");

        return new CemeteryPath
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = cemeteryId,
            Name = LayoutName.Require(name, NameMaxLength, "path.name_invalid"),
            Line = line,
        };
    }

    public void Stamp(DateTimeOffset at) => UpdatedAt = at;
}

public static class LayoutName
{
    public static string Require(string? raw, int max, string code)
    {
        var name = raw?.Trim() ?? "";
        if (name.Length < 1 || name.Length > max)
            throw new DomainRuleException(code);

        return name;
    }
}

public interface IStampable
{
    void Stamp(DateTimeOffset at);
}

using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Layout;

public readonly record struct GeoPoint(double Longitude, double Latitude);

public sealed class GeoPolygon
{
    public const int MinimumRingLength = 4;

    private GeoPolygon(IReadOnlyList<GeoPoint> ring) => Ring = ring;

    public IReadOnlyList<GeoPoint> Ring { get; }

    public static GeoPolygon Create(IReadOnlyList<GeoPoint> ring)
    {
        if (ring.Count < MinimumRingLength)
            throw new DomainRuleException("geometry.ring_invalid");
        if (ring[0] != ring[^1])
            throw new DomainRuleException("geometry.ring_open");

        foreach (var point in ring)
        {
            if (point.Longitude is < -180 or > 180 || point.Latitude is < -90 or > 90)
                throw new DomainRuleException("geometry.coordinate_invalid");
        }

        if (SignedArea(ring) == 0)
            throw new DomainRuleException("geometry.ring_empty");

        return new GeoPolygon(ring);
    }

    public bool InteriorOverlaps(GeoPolygon other) =>
        EdgesCross(Ring, other.Ring)
        || AnyStrictlyInside(Ring, other.Ring)
        || AnyStrictlyInside(other.Ring, Ring);

    private static bool AnyStrictlyInside(IReadOnlyList<GeoPoint> points, IReadOnlyList<GeoPoint> ring)
    {
        for (var i = 0; i < points.Count - 1; i++)
        {
            if (StrictlyInside(points[i], ring))
                return true;
            var midpoint = new GeoPoint(
                (points[i].Longitude + points[i + 1].Longitude) / 2,
                (points[i].Latitude + points[i + 1].Latitude) / 2);
            if (StrictlyInside(midpoint, ring))
                return true;
        }

        return false;
    }

    public static (GeoPolygon Left, GeoPolygon Right) Split(GeoPolygon polygon)
    {
        var minLon = polygon.Ring.Min(point => point.Longitude);
        var maxLon = polygon.Ring.Max(point => point.Longitude);
        var minLat = polygon.Ring.Min(point => point.Latitude);
        var maxLat = polygon.Ring.Max(point => point.Latitude);
        var mid = (minLon + maxLon) / 2;
        if (mid <= minLon || mid >= maxLon)
            throw new DomainRuleException("grave_site.split_invalid");

        return (Rectangle(minLon, mid, minLat, maxLat), Rectangle(mid, maxLon, minLat, maxLat));
    }

    public static GeoPolygon BoundsOf(GeoPolygon left, GeoPolygon right)
    {
        var points = left.Ring.Concat(right.Ring);
        return Rectangle(
            points.Min(point => point.Longitude),
            points.Max(point => point.Longitude),
            points.Min(point => point.Latitude),
            points.Max(point => point.Latitude));
    }

    private static GeoPolygon Rectangle(double minLon, double maxLon, double minLat, double maxLat) =>
        Create([
            new GeoPoint(minLon, minLat),
            new GeoPoint(maxLon, minLat),
            new GeoPoint(maxLon, maxLat),
            new GeoPoint(minLon, maxLat),
            new GeoPoint(minLon, minLat),
        ]);

    private static bool EdgesCross(IReadOnlyList<GeoPoint> left, IReadOnlyList<GeoPoint> right)
    {
        for (var i = 0; i < left.Count - 1; i++)
        {
            for (var j = 0; j < right.Count - 1; j++)
            {
                if (ProperCross(left[i], left[i + 1], right[j], right[j + 1]))
                    return true;
            }
        }

        return false;
    }

    private static bool ProperCross(GeoPoint a, GeoPoint b, GeoPoint c, GeoPoint d)
    {
        var first = Orient(a, b, c) * Orient(a, b, d);
        var second = Orient(c, d, a) * Orient(c, d, b);
        return first < 0 && second < 0;
    }

    private static int Orient(GeoPoint a, GeoPoint b, GeoPoint c)
    {
        var value = (b.Longitude - a.Longitude) * (c.Latitude - a.Latitude)
            - (b.Latitude - a.Latitude) * (c.Longitude - a.Longitude);
        if (Math.Abs(value) < 1e-12)
            return 0;
        return value > 0 ? 1 : -1;
    }

    private static bool StrictlyInside(GeoPoint point, IReadOnlyList<GeoPoint> ring)
    {
        if (OnBoundary(point, ring))
            return false;

        var inside = false;
        for (var i = 0; i < ring.Count - 1; i++)
        {
            var start = ring[i];
            var end = ring[i + 1];
            if (!RayCrosses(point, start, end))
                continue;
            inside = !inside;
        }

        return inside;
    }

    private static bool RayCrosses(GeoPoint point, GeoPoint start, GeoPoint end)
    {
        if ((start.Latitude > point.Latitude) == (end.Latitude > point.Latitude) || end.Latitude == start.Latitude)
            return false;

        var crossing = start.Longitude
            + (point.Latitude - start.Latitude) / (end.Latitude - start.Latitude) * (end.Longitude - start.Longitude);
        return point.Longitude < crossing;
    }

    private static bool OnBoundary(GeoPoint point, IReadOnlyList<GeoPoint> ring)
    {
        for (var i = 0; i < ring.Count - 1; i++)
        {
            if (Orient(ring[i], ring[i + 1], point) == 0 && Within(point, ring[i], ring[i + 1]))
                return true;
        }

        return false;
    }

    private static bool Within(GeoPoint point, GeoPoint start, GeoPoint end)
    {
        var minLon = Math.Min(start.Longitude, end.Longitude) - 1e-12;
        var maxLon = Math.Max(start.Longitude, end.Longitude) + 1e-12;
        var minLat = Math.Min(start.Latitude, end.Latitude) - 1e-12;
        var maxLat = Math.Max(start.Latitude, end.Latitude) + 1e-12;
        return point.Longitude >= minLon && point.Longitude <= maxLon && point.Latitude >= minLat && point.Latitude <= maxLat;
    }

    private static double SignedArea(IReadOnlyList<GeoPoint> ring)
    {
        double sum = 0;
        for (var i = 0; i < ring.Count - 1; i++)
            sum += ring[i].Longitude * ring[i + 1].Latitude - ring[i + 1].Longitude * ring[i].Latitude;
        return sum / 2;
    }
}

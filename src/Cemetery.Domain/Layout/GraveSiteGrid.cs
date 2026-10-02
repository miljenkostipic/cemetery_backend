using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Layout;

public static class GraveSiteGrid
{
    public const int MaxSites = 2000;
    private const double MetersPerDegreeLatitude = 111_320;

    public static IReadOnlyList<GeoPolygon> Generate(
        GeoPoint origin,
        int rows,
        int columns,
        double widthMeters,
        double depthMeters,
        double gapMeters,
        double rotationDegrees)
    {
        if (rows < 1 || columns < 1 || rows * (long)columns > MaxSites)
            throw new DomainRuleException("grave_site.grid_invalid");
        if (widthMeters <= 0 || depthMeters <= 0 || gapMeters < 0)
            throw new DomainRuleException("grave_site.grid_invalid");

        var metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(origin.Latitude * Math.PI / 180);
        if (Math.Abs(metersPerDegreeLongitude) < 1)
            throw new DomainRuleException("grave_site.grid_invalid");

        var angle = rotationDegrees * Math.PI / 180;
        return Cells(origin, rows, columns, widthMeters, depthMeters, gapMeters, Math.Cos(angle), Math.Sin(angle), metersPerDegreeLongitude);
    }

    private static List<GeoPolygon> Cells(
        GeoPoint origin,
        int rows,
        int columns,
        double width,
        double depth,
        double gap,
        double cos,
        double sin,
        double metersPerDegreeLongitude)
    {
        var cells = new List<GeoPolygon>(rows * columns);
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var east = column * (width + gap);
                var north = row * (depth + gap);
                cells.Add(Cell(origin, east, north, width, depth, cos, sin, metersPerDegreeLongitude));
            }
        }

        return cells;
    }

    private static GeoPolygon Cell(
        GeoPoint origin,
        double east,
        double north,
        double width,
        double depth,
        double cos,
        double sin,
        double metersPerDegreeLongitude)
    {
        var southwest = Shift(origin, east, north, cos, sin, metersPerDegreeLongitude);
        var southeast = Shift(origin, east + width, north, cos, sin, metersPerDegreeLongitude);
        var northeast = Shift(origin, east + width, north + depth, cos, sin, metersPerDegreeLongitude);
        var northwest = Shift(origin, east, north + depth, cos, sin, metersPerDegreeLongitude);
        return GeoPolygon.Create([southwest, southeast, northeast, northwest, southwest]);
    }

    private static GeoPoint Shift(
        GeoPoint origin,
        double east,
        double north,
        double cos,
        double sin,
        double metersPerDegreeLongitude)
    {
        var rotatedEast = east * cos - north * sin;
        var rotatedNorth = east * sin + north * cos;
        return new GeoPoint(
            origin.Longitude + rotatedEast / metersPerDegreeLongitude,
            origin.Latitude + rotatedNorth / MetersPerDegreeLatitude);
    }
}

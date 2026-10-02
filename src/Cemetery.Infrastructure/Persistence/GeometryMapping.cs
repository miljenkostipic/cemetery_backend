using Cemetery.Domain.Layout;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NetTopologySuite.Geometries;

namespace Cemetery.Infrastructure.Persistence;

internal static class GeometryMapping
{
    private static readonly GeometryFactory Factory = new(new PrecisionModel(), 4326);

    public static readonly ValueConverter<GeoPolygon?, Polygon?> PolygonConverter = new(
        polygon => ToPolygon(polygon),
        polygon => FromPolygon(polygon));

    public static Polygon? ToPolygon(GeoPolygon? polygon)
    {
        if (polygon is null)
            return null;

        var coordinates = polygon.Ring.Select(point => new Coordinate(point.Longitude, point.Latitude)).ToArray();
        return Factory.CreatePolygon(coordinates);
    }

    public static GeoPolygon? FromPolygon(Polygon? polygon)
    {
        if (polygon is null)
            return null;

        var ring = polygon.ExteriorRing.Coordinates
            .Select(coordinate => new GeoPoint(coordinate.X, coordinate.Y))
            .ToArray();
        return GeoPolygon.Create(ring);
    }
}

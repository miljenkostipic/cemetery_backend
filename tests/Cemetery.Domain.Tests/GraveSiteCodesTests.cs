using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;

namespace Cemetery.Domain.Tests;

public sealed class GraveSiteCodesTests
{
    private const double MetersPerDegreeLatitude = 111_320;
    private static readonly HashSet<string> Nothing = new(StringComparer.Ordinal);

    [Theory]
    [InlineData("A", "3", 12, "A-03-12")]
    [InlineData("A", "3", 100, "A-03-100")]
    [InlineData("A", "123", 1, "A-123-01")]
    [InlineData("A", " 7 ", 4, "A-07-04")]
    [InlineData("A", "B", 1, "A-B-01")]
    [InlineData("A", "12a", 2, "A-12a-02")]
    public void Graves_in_a_row_read_field_row_place(string section, string row, int place, string expected) =>
        Assert.Equal(expected, GraveSiteCodes.InRow(section, row, place));

    [Theory]
    [InlineData("C", 47, "C-047")]
    [InlineData("C", 1, "C-001")]
    [InlineData("C", 1000, "C-1000")]
    public void Row_less_graves_read_field_number(string section, int number, string expected) =>
        Assert.Equal(expected, GraveSiteCodes.Loose(section, number));

    [Fact]
    public void The_first_place_in_an_empty_row_is_one() =>
        Assert.Equal("A-03-01", GraveSiteCodes.NextInRow("A", "3", [], Nothing));

    [Fact]
    public void The_next_place_follows_the_highest_and_ignores_other_shapes()
    {
        string[] row = ["A-03-01", "A-03-05", "A-03-02-L", "A-0001", "B-03-09", "A-03-", "A-03-1234567890"];

        Assert.Equal("A-03-06", GraveSiteCodes.NextInRow("A", "3", row, Nothing));
    }

    [Fact]
    public void The_next_place_skips_a_code_already_used_elsewhere()
    {
        var taken = new HashSet<string>(["A-03-02", "A-03-03"], StringComparer.Ordinal);

        Assert.Equal("A-03-04", GraveSiteCodes.NextInRow("A", "3", ["A-03-01"], taken));
    }

    [Fact]
    public void The_next_row_less_number_follows_the_highest()
    {
        string[] loose = ["C-047", "C-012", "C-012-L", "C-x", "D-100"];

        Assert.Equal("C-048", GraveSiteCodes.NextLoose("C", loose, Nothing));
        Assert.Equal("C-001", GraveSiteCodes.NextLoose("C", [], Nothing));
    }

    [Fact]
    public void The_next_row_less_number_skips_a_code_already_used_elsewhere()
    {
        var taken = new HashSet<string>(["C-048"], StringComparer.Ordinal);

        Assert.Equal("C-049", GraveSiteCodes.NextLoose("C", ["C-047"], taken));
    }

    [Fact]
    public void Area_is_measured_in_square_metres()
    {
        var outline = Box(1, 2);

        Assert.InRange(outline.AreaSquareMeters(), 1.99, 2.01);
    }

    [Fact]
    public void A_drawn_grave_of_a_believable_size_is_accepted()
    {
        GraveSite.RejectDrawnShape(Box(1, 2));
        GraveSite.RejectDrawnShape(Circle(1.5, GraveSite.OutlineMaxPoints - 1));
    }

    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(15, 15)]
    public void A_drawn_grave_too_small_or_too_large_is_refused(double width, double depth)
    {
        var error = Assert.Throws<DomainRuleException>(() => GraveSite.RejectDrawnShape(Box(width, depth)));

        Assert.Equal("grave_site.size_invalid", error.Code);
    }

    [Fact]
    public void A_drawn_grave_with_too_many_points_is_refused()
    {
        var error = Assert.Throws<DomainRuleException>(() => GraveSite.RejectDrawnShape(Circle(1.5, GraveSite.OutlineMaxPoints)));

        Assert.Equal("grave_site.outline_too_complex", error.Code);
    }

    [Fact]
    public void A_grave_must_reach_into_its_section_outline()
    {
        var outlined = Section.Add(Guid.CreateVersion7(), Guid.CreateVersion7(), "Field A", "A", Box(20, 20));
        var open = Section.Add(Guid.CreateVersion7(), Guid.CreateVersion7(), "Field B", "B", null);
        var far = Box(1, 2, 100);

        outlined.RejectOutside(Box(1, 2));
        open.RejectOutside(far);
        var error = Assert.Throws<DomainRuleException>(() => outlined.RejectOutside(far));
        Assert.Equal("grave_site.outside_section", error.Code);
    }

    private static GeoPolygon Box(double width, double depth, double eastOffset = 0)
    {
        const double latitude = 45;
        var metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(latitude * Math.PI / 180);
        var west = 16 + eastOffset / metersPerDegreeLongitude;
        var east = west + width / metersPerDegreeLongitude;
        var north = latitude + depth / MetersPerDegreeLatitude;
        return GeoPolygon.Create([
            new GeoPoint(west, latitude),
            new GeoPoint(east, latitude),
            new GeoPoint(east, north),
            new GeoPoint(west, north),
            new GeoPoint(west, latitude),
        ]);
    }

    /// <summary>A circle as a closed ring of <paramref name="vertices"/> + 1 points.</summary>
    private static GeoPolygon Circle(double radius, int vertices)
    {
        const double latitude = 45;
        var metersPerDegreeLongitude = MetersPerDegreeLatitude * Math.Cos(latitude * Math.PI / 180);
        var ring = Enumerable.Range(0, vertices)
            .Select(index => 2 * Math.PI * index / vertices)
            .Select(angle => new GeoPoint(16 + radius * Math.Cos(angle) / metersPerDegreeLongitude, latitude + radius * Math.Sin(angle) / MetersPerDegreeLatitude))
            .ToList();
        ring.Add(ring[0]);
        return GeoPolygon.Create(ring);
    }
}

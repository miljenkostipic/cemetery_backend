using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;

namespace Cemetery.Domain.Tests;

public sealed class LayoutRulesTests
{
    [Fact]
    public void Kinds_define_a_default_capacity()
    {
        Assert.Equal(1, GraveSiteKinds.DefaultCapacity(GraveSiteKind.SingleGrave));
        Assert.Equal(2, GraveSiteKinds.DefaultCapacity(GraveSiteKind.Family));
        Assert.Equal(6, GraveSiteKinds.DefaultCapacity(GraveSiteKind.Tomb));
        Assert.Equal(0, GraveSiteKinds.DefaultCapacity(GraveSiteKind.Memorial));
    }

    [Fact]
    public void Status_is_derived_from_geometry_and_occupancy()
    {
        Assert.Equal(GraveSiteStatus.Planned, GraveSiteStatuses.Derive(false, false, false, 0, 1, false));
        Assert.Equal(GraveSiteStatus.Available, GraveSiteStatuses.Derive(false, true, false, 0, 1, false));
        Assert.Equal(GraveSiteStatus.Reserved, GraveSiteStatuses.Derive(false, true, true, 0, 1, false));
        Assert.Equal(GraveSiteStatus.Occupied, GraveSiteStatuses.Derive(false, true, false, 1, 2, false));
        Assert.Equal(GraveSiteStatus.Full, GraveSiteStatuses.Derive(false, true, false, 2, 2, false));
        Assert.Equal(GraveSiteStatus.Reusable, GraveSiteStatuses.Derive(false, true, false, 0, 1, true));
        Assert.Equal(GraveSiteStatus.Closed, GraveSiteStatuses.Derive(true, true, false, 1, 1, false));
    }

    [Fact]
    public void Adjacent_polygons_do_not_overlap()
    {
        var left = Rectangle(16, 16.001, 45, 45.001);
        var right = Rectangle(16.001, 16.002, 45, 45.001);

        Assert.False(left.InteriorOverlaps(right));
    }

    [Fact]
    public void Overlapping_polygons_are_rejected()
    {
        var left = Rectangle(16, 16.002, 45, 45.001);
        var right = Rectangle(16.001, 16.003, 45, 45.001);

        Assert.True(left.InteriorOverlaps(right));
    }

    [Fact]
    public void A_grid_does_not_overlap_itself()
    {
        var cells = GraveSiteGrid.Generate(new GeoPoint(16, 45), 2, 2, 2, 1, 0.2, 15);

        Assert.Equal(4, cells.Count);
        Assert.False(cells[0].InteriorOverlaps(cells[1]));
        Assert.False(cells[0].InteriorOverlaps(cells[2]));
    }

    [Fact]
    public void Split_halves_share_an_edge_only()
    {
        var (left, right) = GeoPolygon.Split(Rectangle(16, 16.002, 45, 45.001));

        Assert.False(left.InteriorOverlaps(right));
    }

    [Fact]
    public void An_open_ring_is_rejected()
    {
        var error = Assert.Throws<DomainRuleException>(() => GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.001, 45),
            new GeoPoint(16.001, 45.001),
            new GeoPoint(16, 45.001),
        ]));

        Assert.Equal("geometry.ring_open", error.Code);
    }

    [Fact]
    public void A_placed_site_has_an_id_type_and_geometry()
    {
        var site = GraveSite.Place(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), null, "A-0001", GraveSiteKind.SingleGrave, Rectangle(16, 16.001, 45, 45.001));

        Assert.NotEqual(Guid.Empty, site.Id);
        Assert.Equal(GraveSiteKind.SingleGrave, site.Kind);
        Assert.Equal(1, site.Capacity);
        Assert.NotNull(site.Outline);
        Assert.Equal(GraveSiteStatus.Available, site.Status);
    }

    [Fact]
    public void A_closed_site_can_be_reopened()
    {
        var site = GraveSite.Place(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), null, "A-0001", GraveSiteKind.SingleGrave, Rectangle(16, 16.001, 45, 45.001));
        site.Close();

        site.Reopen();

        Assert.False(site.Closed);
        Assert.Equal(GraveSiteStatus.Available, site.Status);
    }

    [Fact]
    public void An_open_site_cannot_be_reset()
    {
        var site = GraveSite.Place(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), null, "A-0001", GraveSiteKind.SingleGrave, Rectangle(16, 16.001, 45, 45.001));

        var error = Assert.Throws<DomainRuleException>(() => site.Reopen());

        Assert.Equal("grave_site.reset_invalid", error.Code);
    }

    [Fact]
    public void Merge_closes_the_sources_and_sums_capacity()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = Guid.CreateVersion7();
        var section = Guid.CreateVersion7();
        var left = GraveSite.Place(tenant, cemetery, section, null, "A-0001", GraveSiteKind.SingleGrave, Rectangle(16, 16.001, 45, 45.001));
        var right = GraveSite.Place(tenant, cemetery, section, null, "A-0002", GraveSiteKind.SingleGrave, Rectangle(16.001, 16.002, 45, 45.001));

        var merged = GraveSite.Merge(left, right, "A-0003");

        Assert.True(left.Closed);
        Assert.True(right.Closed);
        Assert.Equal(2, merged.Capacity);
        Assert.Equal(GraveSiteStatus.Available, merged.Status);
    }

    private static GeoPolygon Rectangle(double minLon, double maxLon, double minLat, double maxLat) =>
        GeoPolygon.Create([
            new GeoPoint(minLon, minLat),
            new GeoPoint(maxLon, minLat),
            new GeoPoint(maxLon, maxLat),
            new GeoPoint(minLon, maxLat),
            new GeoPoint(minLon, minLat),
        ]);
}

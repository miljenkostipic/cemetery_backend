using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;

namespace Cemetery.Domain.Tests;

public sealed class GraveSiteAndAuditRulesTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();

    private static readonly Guid CemeteryId = Guid.CreateVersion7();

    private static readonly Guid SectionId = Guid.CreateVersion7();

    private static readonly DateTimeOffset At = new(2026, 11, 6, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(AuditActions.DeceasedRecorded)]
    [InlineData(AuditActions.DeceasedCorrected)]
    [InlineData(AuditActions.DeceasedRemoved)]
    [InlineData(AuditActions.IntermentRecorded)]
    [InlineData(AuditActions.IntermentExhumed)]
    [InlineData(AuditActions.IntermentTransferred)]
    [InlineData(AuditActions.RestPeriodSet)]
    [InlineData(AuditActions.RegisterPublished)]
    [InlineData(AuditActions.RegisterWithdrawn)]
    [InlineData(AuditActions.GraveHidden)]
    [InlineData(AuditActions.GraveShown)]
    [InlineData(AuditActions.MemorialDescribed)]
    public void Every_known_audit_action_can_be_written(string action)
    {
        Assert.True(AuditActions.IsKnown(action));

        var entry = AuditEntry.Write(Tenant, action, Guid.CreateVersion7(), null, At);

        Assert.Equal(action, entry.Action);
        Assert.Equal(action, entry.Reason);
        Assert.Equal(At, entry.At);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("deceased")]
    [InlineData("deceased.renamed")]
    [InlineData("interment.moved")]
    [InlineData("register.archived")]
    [InlineData("grave_site.split")]
    [InlineData("memorial.removed")]
    [InlineData("cemetery.renamed")]
    public void Unknown_audit_actions_are_refused(string? action)
    {
        Assert.False(AuditActions.IsKnown(action));
        Assert.Equal("audit.invalid", Code(() => AuditEntry.Write(Tenant, action!, Guid.CreateVersion7(), null, At)));
    }

    [Fact]
    public void An_audit_entry_needs_a_tenant_a_subject_and_a_short_reason()
    {
        var subject = Guid.CreateVersion7();

        Assert.Equal("Moved by family", AuditEntry.Write(Tenant, AuditActions.IntermentTransferred, subject, " Moved by family ", At).Reason);
        Assert.Equal("audit.invalid", Code(() => AuditEntry.Write(Guid.Empty, AuditActions.DeceasedRecorded, subject, null, At)));
        Assert.Equal("audit.invalid", Code(() => AuditEntry.Write(Tenant, AuditActions.DeceasedRecorded, Guid.Empty, null, At)));
        Assert.Equal("audit.invalid", Code(() => AuditEntry.Write(Tenant, AuditActions.DeceasedRecorded, subject, new string('a', CorrectionReason.MaxLength + 1), At)));
    }

    [Fact]
    public void A_grave_site_needs_a_place_a_code_and_a_fitting_capacity()
    {
        var outline = Rectangle(16, 16.001);

        var site = GraveSite.Place(Tenant, CemeteryId, SectionId, null, " A-1 ", GraveSiteKind.Family, outline, 3);
        var memorial = GraveSite.Place(Tenant, CemeteryId, SectionId, null, "M-1", GraveSiteKind.Memorial, outline);

        Assert.Equal("A-1", site.Code);
        Assert.Equal(3, site.Capacity);
        Assert.Equal(0, memorial.Capacity);
        Assert.Equal("tenant.required", Code(() => GraveSite.Place(Guid.Empty, CemeteryId, SectionId, null, "A", GraveSiteKind.SingleGrave, outline)));
        Assert.Equal("grave_site.location_required", Code(() => GraveSite.Place(Tenant, Guid.Empty, SectionId, null, "A", GraveSiteKind.SingleGrave, outline)));
        Assert.Equal("grave_site.location_required", Code(() => GraveSite.Place(Tenant, CemeteryId, Guid.Empty, null, "A", GraveSiteKind.SingleGrave, outline)));
        Assert.Equal("grave_site.code_invalid", Code(() => GraveSite.Place(Tenant, CemeteryId, SectionId, null, " ", GraveSiteKind.SingleGrave, outline)));
        Assert.Equal("grave_site.code_invalid", Code(() => GraveSite.Place(Tenant, CemeteryId, SectionId, null, new string('A', GraveSite.CodeMaxLength + 1), GraveSiteKind.SingleGrave, outline)));
        Assert.Equal("grave_site.capacity_invalid", Code(() => GraveSite.Place(Tenant, CemeteryId, SectionId, null, "A", GraveSiteKind.SingleGrave, outline, -1)));
        Assert.Equal("grave_site.capacity_invalid", Code(() => GraveSite.Place(Tenant, CemeteryId, SectionId, null, "A", GraveSiteKind.SingleGrave, outline, 0)));
        Assert.Equal("grave_site.capacity_invalid", Code(() => GraveSite.Place(Tenant, CemeteryId, SectionId, null, "A", GraveSiteKind.Memorial, outline, 1)));
    }

    [Fact]
    public void A_grave_site_reports_status_from_its_use()
    {
        var site = Site("A-1", 16, 16.001, GraveSiteKind.Family);

        Assert.Equal(GraveSiteStatus.Available, site.StatusFor(new SiteUse(0, false)));
        Assert.Equal(GraveSiteStatus.Occupied, site.StatusFor(new SiteUse(1, false)));
        Assert.Equal(GraveSiteStatus.Full, site.StatusFor(new SiteUse(2, false)));
        Assert.Equal(GraveSiteStatus.Reusable, site.StatusFor(new SiteUse(0, true)));

        site.Apply(new OccupancyStamp(1, GraveSiteStatus.Occupied, new DateOnly(2040, 1, 1)));
        site.Stamp(At);

        Assert.Equal(1, site.Used);
        Assert.Equal(GraveSiteStatus.Occupied, site.Status);
        Assert.Equal(new DateOnly(2040, 1, 1), site.RestUntil);
        Assert.Equal(At, site.UpdatedAt);
    }

    [Fact]
    public void Reopening_a_grave_site_restores_its_occupancy_status()
    {
        var site = Site("A-1", 16, 16.001, GraveSiteKind.Family);
        site.Apply(new OccupancyStamp(1, GraveSiteStatus.Occupied, null));
        site.Close();
        site.Reopen();
        Assert.Equal(GraveSiteStatus.Occupied, site.Status);

        site.Apply(new OccupancyStamp(2, GraveSiteStatus.Full, null));
        site.Close();
        site.Reopen();
        Assert.Equal(GraveSiteStatus.Full, site.Status);
    }

    [Fact]
    public void A_closed_grave_site_keeps_its_outline_and_cannot_be_split()
    {
        var site = Site("A-1", 16, 16.002, GraveSiteKind.SingleGrave);
        var outline = Rectangle(16, 16.003);

        site.ReplaceOutline(outline);
        Assert.Same(outline, site.Outline);

        site.HideFromPublic();
        Assert.True(site.HiddenFromPublic);
        site.ShowOnPublicMap();
        Assert.False(site.HiddenFromPublic);

        var (left, right) = site.Split("A-1a", "A-1b");
        Assert.True(site.Closed);
        Assert.Equal("A-1a", left.Code);
        Assert.Equal("A-1b", right.Code);
        Assert.Equal(site.Capacity, left.Capacity);
        Assert.Equal("grave_site.closed", Code(() => site.ReplaceOutline(outline)));
        Assert.Equal("grave_site.split_invalid", Code(() => site.Split("x", "y")));
    }

    [Fact]
    public void Merging_needs_two_open_neighbours_of_the_same_kind()
    {
        var left = Site("A-1", 16, 16.001, GraveSiteKind.SingleGrave);
        var otherKind = Site("A-2", 16.001, 16.002, GraveSiteKind.Family);
        var otherSection = GraveSite.Place(Tenant, CemeteryId, Guid.CreateVersion7(), null, "B-1", GraveSiteKind.SingleGrave, Rectangle(16.001, 16.002));
        var otherTenant = GraveSite.Place(Guid.CreateVersion7(), CemeteryId, SectionId, null, "C-1", GraveSiteKind.SingleGrave, Rectangle(16.001, 16.002));
        var closed = Site("A-3", 16.001, 16.002, GraveSiteKind.SingleGrave);
        closed.Close();

        Assert.Equal("grave_site.merge_invalid", Code(() => GraveSite.Merge(left, otherKind, "M")));
        Assert.Equal("grave_site.merge_invalid", Code(() => GraveSite.Merge(left, otherSection, "M")));
        Assert.Equal("grave_site.merge_invalid", Code(() => GraveSite.Merge(left, otherTenant, "M")));
        Assert.Equal("grave_site.merge_invalid", Code(() => GraveSite.Merge(left, closed, "M")));
        Assert.Equal("grave_site.merge_invalid", Code(() => GraveSite.Merge(closed, left, "M")));
        Assert.Equal("grave_site.merge_invalid", Code(() => GraveSite.Merge(left, left, "M")));
        Assert.False(left.Closed);
    }

    [Fact]
    public void Merging_memorials_keeps_them_without_capacity()
    {
        var left = Site("M-1", 16, 16.001, GraveSiteKind.Memorial);
        var right = Site("M-2", 16.001, 16.002, GraveSiteKind.Memorial);

        var merged = GraveSite.Merge(left, right, "M-3");

        Assert.Equal(0, merged.Capacity);
        Assert.True(left.Closed);
        Assert.True(right.Closed);
    }

    [Fact]
    public void Overlap_checks_skip_the_ignored_and_closed_sites()
    {
        var existing = Site("A-1", 16, 16.002, GraveSiteKind.SingleGrave);
        var neighbour = Site("A-2", 16.002, 16.003, GraveSiteKind.SingleGrave);
        var closed = Site("A-3", 16, 16.002, GraveSiteKind.SingleGrave);
        closed.Close();
        var candidate = Rectangle(16.001, 16.0015);

        Assert.Equal("grave_site.overlaps", Code(() => GraveSite.RejectOverlap(candidate, [neighbour, existing])));
        GraveSite.RejectOverlap(candidate, [existing, closed, neighbour], existing.Id);
        GraveSite.RejectOverlap(candidate, []);
    }

    [Fact]
    public void Grave_site_kind_codes_round_trip()
    {
        foreach (var kind in Enum.GetValues<GraveSiteKind>())
        {
            Assert.True(GraveSiteKinds.TryParse(GraveSiteKinds.ToCode(kind), out var parsed));
            Assert.Equal(kind, parsed);
            Assert.True(GraveSiteKinds.DefaultCapacity(kind) >= 0);
        }

        Assert.False(GraveSiteKinds.TryParse("crypt", out _));
        Assert.False(GraveSiteKinds.TryParse(null, out _));
        Assert.Equal("grave_site.kind_invalid", Code(() => GraveSiteKinds.ToCode((GraveSiteKind)99)));
        Assert.Equal("grave_site.kind_invalid", Code(() => GraveSiteKinds.DefaultCapacity((GraveSiteKind)99)));
    }

    [Fact]
    public void Grave_site_status_codes_cover_every_status()
    {
        Assert.Equal(GraveSiteStatus.Closed, GraveSiteStatuses.Derive(true, true, false, 0, 1, false));
        foreach (var status in Enum.GetValues<GraveSiteStatus>())
            Assert.False(string.IsNullOrEmpty(GraveSiteStatuses.ToCode(status)));

        Assert.Equal("grave_site.status_invalid", Code(() => GraveSiteStatuses.ToCode((GraveSiteStatus)99)));
    }

    [Fact]
    public void Layout_names_are_trimmed_and_bounded()
    {
        Assert.Equal("North", LayoutName.Require(" North ", 10, "name.invalid"));
        Assert.Equal("name.invalid", Code(() => LayoutName.Require(null, 10, "name.invalid")));
        Assert.Equal("name.invalid", Code(() => LayoutName.Require("  ", 10, "name.invalid")));
        Assert.Equal("name.invalid", Code(() => LayoutName.Require(new string('a', 11), 10, "name.invalid")));
    }

    private static GraveSite Site(string code, double minLon, double maxLon, GraveSiteKind kind) =>
        GraveSite.Place(Tenant, CemeteryId, SectionId, null, code, kind, Rectangle(minLon, maxLon));

    private static GeoPolygon Rectangle(double minLon, double maxLon) =>
        GeoPolygon.Create([
            new GeoPoint(minLon, 45),
            new GeoPoint(maxLon, 45),
            new GeoPoint(maxLon, 45.001),
            new GeoPoint(minLon, 45.001),
            new GeoPoint(minLon, 45),
        ]);

    private static string Code(Action action) => Assert.Throws<DomainRuleException>(action).Code;

    private static string Code(Func<object?> action) => Assert.Throws<DomainRuleException>(() => action()).Code;
}

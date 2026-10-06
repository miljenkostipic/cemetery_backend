using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;

namespace Cemetery.Domain.Tests;

public sealed class RegisterRulesTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_deceased_record_needs_a_name_and_ordered_dates()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = Guid.CreateVersion7();
        var recorded = Deceased.Record(tenant, cemetery, " Ana ", "Horvat", new DateOnly(1920, 1, 2), new DateOnly(2020, 3, 4), At);

        Assert.Equal("Ana", recorded.GivenName);
        Assert.Equal(1, recorded.Version);
        var blank = Assert.Throws<DomainRuleException>(() => Deceased.Record(tenant, cemetery, " ", "Horvat", null, null, At));
        Assert.Equal("deceased.given_name_invalid", blank.Code);
        var family = Assert.Throws<DomainRuleException>(() => Deceased.Record(tenant, cemetery, "Ana", " ", null, null, At));
        Assert.Equal("deceased.family_name_invalid", family.Code);
        var missingPlace = Assert.Throws<DomainRuleException>(() => Deceased.Record(Guid.Empty, cemetery, "Ana", "Horvat", null, null, At));
        Assert.Equal("deceased.location_required", missingPlace.Code);
        var dates = Assert.Throws<DomainRuleException>(() => Deceased.Record(tenant, cemetery, "Ana", "Horvat", new DateOnly(2021, 1, 1), new DateOnly(2020, 1, 1), At));
        Assert.Equal("deceased.dates_invalid", dates.Code);
    }

    [Fact]
    public void A_correction_stores_a_new_version_and_a_reason()
    {
        var person = Person();
        var missing = Assert.Throws<DomainRuleException>(() => person.Correct("Ana", "Kovač", null, null, " ", At));
        Assert.Equal("deceased.reason_required", missing.Code);

        var revision = person.Correct("Ana", "Kovač", new DateOnly(1920, 1, 2), new DateOnly(2020, 3, 4), " Marriage record ", At);

        Assert.Equal(2, person.Version);
        Assert.Equal("Kovač", person.FamilyName);
        Assert.Equal(2, revision.Version);
        Assert.Equal("Marriage record", revision.Reason);
    }

    [Fact]
    public void An_urn_niche_accepts_only_an_urn()
    {
        var (person, niche) = Pair(GraveSiteKind.UrnNiche);
        var coffin = Assert.Throws<DomainRuleException>(() => Place(person, niche, IntermentKind.Coffin, new DateOnly(2020, 4, 1)));
        Assert.Equal("interment.kind_invalid", coffin.Code);

        var urn = Place(person, niche, IntermentKind.Urn, new DateOnly(2020, 4, 1));

        Assert.Equal(IntermentKind.Urn, urn.Kind);
        Assert.Equal("urn", IntermentKinds.ToCode(urn.Kind));
        Assert.Equal("coffin", IntermentKinds.ToCode(IntermentKind.Coffin));
        Assert.True(urn.Occupies);

        var (bones, ossuary) = Pair(GraveSiteKind.Ossuary);
        var coffinInOssuary = Assert.Throws<DomainRuleException>(() => Place(bones, ossuary, IntermentKind.Coffin, new DateOnly(2020, 4, 1)));
        Assert.Equal("interment.kind_invalid", coffinInOssuary.Code);
    }

    [Fact]
    public void A_memorial_and_a_closed_site_cannot_take_an_interment()
    {
        var (person, memorial) = Pair(GraveSiteKind.Memorial, 0);
        var refused = Assert.Throws<DomainRuleException>(() => Place(person, memorial, IntermentKind.Urn, new DateOnly(2020, 4, 1), 1));
        Assert.Equal("interment.kind_invalid", refused.Code);

        var (buried, grave) = Pair();
        grave.Close();
        var closed = Assert.Throws<DomainRuleException>(() => Place(buried, grave, IntermentKind.Coffin, new DateOnly(2020, 4, 1)));
        Assert.Equal("grave_site.closed", closed.Code);

        var outsider = Person();
        var mismatch = Assert.Throws<DomainRuleException>(() => Place(outsider, grave, IntermentKind.Coffin, new DateOnly(2020, 4, 1)));
        Assert.Equal("interment.location_invalid", mismatch.Code);
        var broken = Assert.Throws<DomainRuleException>(() => Interment.Record(Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, 1, IntermentKind.Coffin, new DateOnly(2020, 4, 1)));
        Assert.Equal("interment.location_invalid", broken.Code);
    }

    [Fact]
    public void A_position_cannot_be_reused_before_the_rest_period()
    {
        var (_, site) = Pair();
        var buried = Place(Earlier(site), site, IntermentKind.Coffin, new DateOnly(2000, 1, 1));
        var next = Another(site);

        var early = Assert.Throws<DomainRuleException>(() =>
            BurialBook.Place(next, site, 1, IntermentKind.Coffin, new DateOnly(2014, 12, 31), [buried], [], RestPeriod.DefaultYears));

        Assert.Equal("interment.position_taken", early.Code);
        Assert.True(buried.Occupies);
    }

    [Fact]
    public void A_position_can_be_reused_after_the_rest_period()
    {
        var (_, site) = Pair();
        var buried = Place(Earlier(site), site, IntermentKind.Coffin, new DateOnly(2000, 1, 1));
        var next = Another(site);

        var reused = BurialBook.Place(next, site, 1, IntermentKind.Coffin, new DateOnly(2015, 1, 1), [buried], [], RestPeriod.DefaultYears);

        Assert.False(buried.Occupies);
        Assert.Equal(new DateOnly(2015, 1, 1), buried.SupersededOn);
        Assert.True(reused.Occupies);
        var use = SiteOccupancy.Summarize(site.Id, site.Capacity, [buried, reused], new DateOnly(2015, 1, 1), RestPeriod.DefaultYears);
        Assert.Equal(1, use.Occupied);
        Assert.False(use.Reusable);
    }

    [Fact]
    public void Exhumation_frees_a_position_before_the_rest_period_ends()
    {
        var (_, site) = Pair();
        var buried = Place(Earlier(site), site, IntermentKind.Coffin, new DateOnly(2000, 1, 1));
        var tooSoon = Assert.Throws<DomainRuleException>(() => buried.Exhume(new DateOnly(1999, 1, 1)));
        Assert.Equal("interment.date_invalid", tooSoon.Code);

        buried.Exhume(new DateOnly(2001, 1, 1));
        var next = Named(site, "Ivo", new DateOnly(2001, 1, 1));
        var again = BurialBook.Place(next, site, 1, IntermentKind.Coffin, new DateOnly(2001, 6, 1), [buried], [], RestPeriod.DefaultYears);

        Assert.False(buried.Occupies);
        Assert.True(again.Occupies);
        var twice = Assert.Throws<DomainRuleException>(() => buried.Exhume(new DateOnly(2002, 1, 1)));
        Assert.Equal("interment.not_active", twice.Code);
    }

    [Fact]
    public void A_person_is_buried_once_and_not_before_death()
    {
        var (person, site) = Pair();
        var buried = Place(person, site, IntermentKind.Coffin, new DateOnly(2020, 4, 1));
        var again = Assert.Throws<DomainRuleException>(() =>
            BurialBook.Place(person, site, 1, IntermentKind.Coffin, new DateOnly(2020, 5, 1), [], [buried], RestPeriod.DefaultYears));
        Assert.Equal("interment.already_buried", again.Code);

        var early = Assert.Throws<DomainRuleException>(() => Place(Another(site), site, IntermentKind.Coffin, new DateOnly(2009, 1, 1)));
        Assert.Equal("interment.date_invalid", early.Code);
    }

    [Fact]
    public void A_transfer_moves_the_remains_to_a_free_position()
    {
        var (person, source) = Pair(GraveSiteKind.Family, 2);
        var buried = Place(person, source, IntermentKind.Coffin, new DateOnly(2020, 4, 1));
        var destination = GraveSite.Place(source.TenantId, source.CemeteryId, source.SectionId, null, "A-0002", GraveSiteKind.SingleGrave, Rectangle(16.002, 16.003, 45, 45.001));

        var same = Assert.Throws<DomainRuleException>(() =>
            BurialBook.Transfer(buried, source, 1, new DateOnly(2021, 1, 1), [buried], RestPeriod.DefaultYears));
        Assert.Equal("interment.transfer_invalid", same.Code);

        var moved = BurialBook.Transfer(buried, destination, 1, new DateOnly(2021, 1, 1), [], RestPeriod.DefaultYears);

        Assert.False(buried.Occupies);
        Assert.Equal(destination.Id, moved.GraveSiteId);
        Assert.Equal(person.Id, moved.DeceasedId);
        Assert.True(moved.Occupies);
    }

    [Fact]
    public void A_removed_person_cannot_be_removed_again()
    {
        var person = Person();
        person.Remove(At);
        Assert.Equal(At, person.RemovedAt);
        var again = Assert.Throws<DomainRuleException>(() => person.Remove(At));
        Assert.Equal("deceased.removed", again.Code);
    }

    [Fact]
    public void The_picker_offers_only_free_and_reusable_positions()
    {
        var (person, site) = Pair(GraveSiteKind.Family, 2);
        var buried = Place(person, site, IntermentKind.Coffin, new DateOnly(2024, 1, 1), 1);
        var on = new DateOnly(2024, 6, 1);

        var open = SiteOccupancy.OpenSlots(site, [buried], on, RestPeriod.DefaultYears);
        var use = SiteOccupancy.Summarize(site.Id, site.Capacity, [buried], on, RestPeriod.DefaultYears);

        Assert.Equal([2], open.Select(slot => slot.Position).ToArray());
        Assert.Equal(PositionState.Free, open[0].State);
        Assert.Equal(1, use.Occupied);
        Assert.False(use.Reusable);
        Assert.Equal("free", PositionStates.ToCode(PositionState.Free));
        Assert.Equal("occupied", PositionStates.ToCode(PositionState.Occupied));
        Assert.Equal("reusable", PositionStates.ToCode(PositionState.Reusable));
    }

    [Fact]
    public void Rest_period_and_audit_entries_reject_invalid_values()
    {
        var years = Assert.Throws<DomainRuleException>(() => RestPeriod.Require(0));
        Assert.Equal("cemetery.rest_period_invalid", years.Code);
        Assert.False(RestPeriod.HasElapsed(new DateOnly(2000, 1, 1), new DateOnly(2014, 12, 31), RestPeriod.DefaultYears));
        Assert.True(RestPeriod.HasElapsed(new DateOnly(2000, 1, 1), new DateOnly(2015, 1, 1), RestPeriod.DefaultYears));

        var audit = AuditEntry.Write(Guid.CreateVersion7(), AuditActions.DeceasedRecorded, Guid.CreateVersion7(), null, At);
        Assert.Equal(AuditActions.DeceasedRecorded, audit.Reason);
        var invalid = Assert.Throws<DomainRuleException>(() => AuditEntry.Write(Guid.Empty, AuditActions.DeceasedRecorded, Guid.CreateVersion7(), null, At));
        Assert.Equal("audit.invalid", invalid.Code);
        Assert.False(IntermentKinds.TryParse("other", out _));
        var kind = Assert.Throws<DomainRuleException>(() => IntermentKinds.Parse("other"));
        Assert.Equal("interment.kind_invalid", kind.Code);
        var state = Assert.Throws<DomainRuleException>(() => PositionStates.ToCode((PositionState)9));
        Assert.Equal("interment.position_invalid", state.Code);
    }

    private static Deceased Person() =>
        Deceased.Record(Guid.CreateVersion7(), Guid.CreateVersion7(), "Ana", "Horvat", null, new DateOnly(2020, 3, 4), At);

    private static (Deceased Person, GraveSite Site) Pair(GraveSiteKind kind = GraveSiteKind.SingleGrave, int? capacity = null)
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = Guid.CreateVersion7();
        var site = GraveSite.Place(tenant, cemetery, Guid.CreateVersion7(), null, "A-0001", kind, Rectangle(16, 16.001, 45, 45.001), capacity);
        return (Deceased.Record(tenant, cemetery, "Ana", "Horvat", new DateOnly(1920, 1, 2), new DateOnly(2020, 3, 4), At), site);
    }

    private static Deceased Another(GraveSite site) =>
        Named(site, "Ivo", new DateOnly(2010, 1, 1));

    private static Deceased Earlier(GraveSite site) =>
        Named(site, "Ana", new DateOnly(1999, 1, 1));

    private static Deceased Named(GraveSite site, string given, DateOnly? diedOn) =>
        Deceased.Record(site.TenantId, site.CemeteryId, given, "Horvat", null, diedOn, At);

    private static Interment Place(Deceased person, GraveSite site, IntermentKind kind, DateOnly buriedOn, int position = 1) =>
        BurialBook.Place(person, site, position, kind, buriedOn, [], [], RestPeriod.DefaultYears);

    private static GeoPolygon Rectangle(double minLon, double maxLon, double minLat, double maxLat) =>
        GeoPolygon.Create([
            new GeoPoint(minLon, minLat),
            new GeoPoint(maxLon, minLat),
            new GeoPoint(maxLon, maxLat),
            new GeoPoint(minLon, maxLat),
            new GeoPoint(minLon, minLat),
        ]);
}

using Cemetery.Domain.Billing;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;
using Cemetery.Domain.Rights;

namespace Cemetery.Domain.Tests;

public sealed class RightsRulesTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly Guid CemeteryId = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 6, 1);

    [Fact]
    public void Shares_must_sum_to_one()
    {
        var holder = Person();
        var whole = RightShare.Of(holder.Id, 100);
        Assert.Equal(1m, whole.Fraction);
        Assert.Equal(whole.HolderId, RightShares.Require([whole])[0].HolderId);

        var error = Assert.Throws<DomainRuleException>(() => RightShares.Require([RightShare.Of(holder.Id, 40), RightShare.Of(Guid.CreateVersion7(), 40)]));
        Assert.Equal("right.share_invalid", error.Code);
    }

    [Fact]
    public void One_active_right_renews_transfers_and_is_inherited()
    {
        var site = Site();
        var first = Holder.Register(Tenant, "Ana", "Kovac", null, null, "ana@example.com", null);
        var next = Holder.Register(Tenant, "Ivo", "Horvat", null, null, "ivo@example.com", null);
        var granted = GraveRight.Grant(Tenant, site.Id, Today, 10, false, [RightShare.Of(first.Id, 100)], null);
        Assert.Equal(new DateOnly(2036, 6, 1), granted.ValidTo);

        var overlap = Assert.Throws<DomainRuleException>(() => GraveRight.Grant(Tenant, site.Id, Today, null, true, [RightShare.Of(next.Id, 100)], granted));
        Assert.Equal("right.overlap", overlap.Code);

        granted.Renew(5);
        Assert.Equal(new DateOnly(2041, 6, 1), granted.ValidTo);

        var transferred = granted.Convey(new DateOnly(2028, 1, 1), [RightShare.Of(next.Id, 60), RightShare.Of(first.Id, 40)]);
        Assert.Equal(GraveRightStatus.Ended, granted.Status);
        Assert.Equal(GraveRightStatus.Active, transferred.Status);
        Assert.Equal(new DateOnly(2041, 6, 1), transferred.ValidTo);
        Assert.Equal(2, transferred.Shares.Count);

        var inherited = transferred.Inherit(new DateOnly(2030, 1, 1), [RightShare.Of(next.Id, 100)]);
        Assert.Equal(GraveRightStatus.Ended, transferred.Status);
        Assert.Equal(next.Id, inherited.Shares[0].HolderId);
    }

    [Fact]
    public void A_perpetual_right_cannot_be_renewed()
    {
        var right = GraveRight.Grant(Tenant, Guid.CreateVersion7(), Today, null, true, [RightShare.Of(Guid.CreateVersion7(), 100)], null);

        var error = Assert.Throws<DomainRuleException>(() => right.Renew(1));

        Assert.Equal("right.perpetual", error.Code);
        Assert.True(right.Perpetual);
    }

    [Fact]
    public void A_reservation_marks_the_site_and_blocks_burial()
    {
        var site = Site();
        var holder = Person();
        var reservation = Reservation.Hold(Tenant, site.Id, holder.Id, Today.AddMonths(1), Today, site.Status);
        site.Apply(SiteOccupancy.Describe(site, true, [], Today, 15));
        Assert.Equal(GraveSiteStatus.Reserved, site.Status);

        var blocked = Assert.Throws<DomainRuleException>(() => BurialGuard.RefuseReserved(true));
        Assert.Equal("reservation.blocks_interment", blocked.Code);

        reservation.Cancel();
        site.Apply(SiteOccupancy.Describe(site, false, [], Today, 15));
        Assert.False(reservation.Active);
        Assert.Equal(GraveSiteStatus.Available, site.Status);
    }

    [Fact]
    public void An_interment_is_stored_on_the_site_row()
    {
        var site = Site();
        var buried = Interment.Record(site.TenantId, site.CemeteryId, Guid.CreateVersion7(), site.Id, 1, IntermentKind.Coffin, new DateOnly(2020, 1, 2));
        site.Apply(SiteOccupancy.Describe(site, false, [buried], Today, 15));

        Assert.Equal(1, site.Used);
        Assert.Equal(GraveSiteStatus.Full, site.Status);
        Assert.Equal(new DateOnly(2035, 1, 2), site.RestUntil);
    }

    [Fact]
    public void A_yearly_run_bills_each_holder_on_the_active_right()
    {
        var site = Site();
        var fee = FeeSchedule.Add(Tenant, site.CemeteryId, "annual", "Annual", "100.00", "EUR");
        var ana = Person();
        var ivo = Holder.Register(Tenant, "Ivo", "Horvat", null, null, "ivo@example.com", null);
        var holders = new Dictionary<Guid, IReadOnlyList<BilledHolder>>
        {
            [site.Id] = [new BilledHolder(ana.Id, ana.DisplayName, 60), new BilledHolder(ivo.Id, ivo.DisplayName, 40)],
        };

        var charges = YearlyBilling.Raise(Tenant, site.CemeteryId, 2026, [fee], [site], new HashSet<ChargeKey>(), holders);

        Assert.Equal(2, charges.Count);
        Assert.Equal(ana.Id, charges[0].HolderId);
        Assert.Equal(60.00m, charges[0].Amount);
        Assert.Equal(ivo.Id, charges[1].HolderId);
        Assert.Equal(40.00m, charges[1].Amount);
    }

    [Theory]
    [InlineData("12345678903")]
    [InlineData(" 69435151530 ")]
    public void A_valid_oib_is_kept(string raw) => Assert.Equal(raw.Trim(), OibNumber.Normalize(raw));

    [Theory]
    [InlineData("12345678901")]
    [InlineData("123")]
    public void A_bad_oib_is_refused(string raw)
    {
        var error = Assert.Throws<DomainRuleException>(() => OibNumber.Normalize(raw));
        Assert.Equal("holder.oib_invalid", error.Code);
    }

    [Fact]
    public void A_holder_links_once_and_a_document_checks_its_bytes()
    {
        var holder = Person();
        var userId = Guid.CreateVersion7();
        holder.Link(userId);
        holder.Link(userId);
        Assert.Equal(userId, holder.UserId);

        var png = DocumentBytes.Decode("contract.png", "image/png", Convert.ToBase64String(new byte[] { 0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4 }));
        Assert.Equal("image/png", png.ContentType);
        var error = Assert.Throws<DomainRuleException>(() => DocumentBytes.Decode("notes.txt", "text/plain", Convert.ToBase64String("hello"u8.ToArray())));
        Assert.Equal("document.type_invalid", error.Code);
    }

    [Fact]
    public void An_entrance_and_a_path_need_a_name_and_geometry()
    {
        var entrance = Entrance.Place(Tenant, CemeteryId, "Gate", new GeoPoint(16, 45));
        var path = CemeteryPath.Draw(Tenant, CemeteryId, "Main", GeoLine.Create([new GeoPoint(16, 45), new GeoPoint(16.001, 45)]));

        Assert.Equal("Gate", entrance.Name);
        Assert.Equal(2, path.Line.Points.Count);
        var error = Assert.Throws<DomainRuleException>(() => GeoLine.Create([new GeoPoint(16, 45)]));
        Assert.Equal("geometry.line_invalid", error.Code);
    }

    private static Holder Person() => Holder.Register(Tenant, "Ana", "Kovac", "12345678903", "Ulica 1", "ana@example.com", "+38591111");

    private static GraveSite Site() =>
        GraveSite.Place(Tenant, CemeteryId, Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.001, 45),
            new GeoPoint(16.001, 45.001),
            new GeoPoint(16, 45.001),
            new GeoPoint(16, 45),
        ]));
}

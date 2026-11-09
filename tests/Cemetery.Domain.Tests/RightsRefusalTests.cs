using System.Text;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Rights;

namespace Cemetery.Domain.Tests;

public sealed class RightsRefusalTests
{
    private const string ValidOib = "12345678903";

    private static readonly Guid Tenant = Guid.CreateVersion7();

    private static readonly DateOnly Start = new(2020, 1, 1);

    [Fact]
    public void A_holder_is_registered_with_normalised_contact_details()
    {
        var holder = Holder.Register(Tenant, " Ana ", "Horvat", $" {ValidOib} ", " Ilica 1 ", " Ana@Example.HR ", " 091 ");

        Assert.Equal("Ana Horvat", holder.DisplayName);
        Assert.Equal(ValidOib, holder.Oib);
        Assert.Equal("Ilica 1", holder.Address);
        Assert.Equal("ana@example.hr", holder.Email);
        Assert.Equal("091", holder.Phone);
        Assert.Equal(Tenant, holder.TenantId);

        var bare = Holder.Register(Tenant, "Ana", "Horvat", " ", null, "", null);
        Assert.Null(bare.Oib);
        Assert.Null(bare.Address);
        Assert.Null(bare.Email);
        Assert.Null(bare.Phone);
    }

    [Fact]
    public void A_holder_refuses_missing_tenant_and_invalid_details()
    {
        Assert.Equal("tenant.required", Code(() => Holder.Register(Guid.Empty, "Ana", "Horvat", null, null, null, null)));
        Assert.Equal("deceased.given_name_invalid", Code(() => Holder.Register(Tenant, " ", "Horvat", null, null, null, null)));
        Assert.Equal("holder.address_invalid", Code(() => Holder.Register(Tenant, "Ana", "Horvat", null, new string('a', Holder.AddressMaxLength + 1), null, null)));
        Assert.Equal("holder.phone_invalid", Code(() => Holder.Register(Tenant, "Ana", "Horvat", null, null, null, new string('1', Holder.PhoneMaxLength + 1))));
        Assert.Throws<DomainRuleException>(() => Holder.Register(Tenant, "Ana", "Horvat", null, null, "not-an-email", null));
    }

    [Fact]
    public void A_holder_correction_replaces_every_detail()
    {
        var holder = Holder.Register(Tenant, "Ana", "Horvat", ValidOib, "Ilica 1", "ana@example.hr", "091");

        holder.Correct("Ana", "Kovač", null, " Vlaška 2 ", null, null);

        Assert.Equal("Kovač", holder.FamilyName);
        Assert.Null(holder.Oib);
        Assert.Equal("Vlaška 2", holder.Address);
        Assert.Null(holder.Email);
        Assert.Null(holder.Phone);
        Assert.Equal("deceased.family_name_invalid", Code(() => holder.Correct("Ana", "", null, null, null, null)));
    }

    [Fact]
    public void A_holder_links_to_one_user_only()
    {
        var holder = Holder.Register(Tenant, "Ana", "Horvat", null, null, null, null);
        var user = Guid.CreateVersion7();

        Assert.Equal("holder.user_invalid", Code(() => holder.Link(Guid.Empty)));
        holder.Link(user);
        holder.Link(user);
        Assert.Equal(user, holder.UserId);
        Assert.Equal("holder.already_linked", Code(() => holder.Link(Guid.CreateVersion7())));
    }

    [Theory]
    [InlineData("12345678900")]
    [InlineData("1234567890")]
    [InlineData("123456789031")]
    [InlineData("1234567890a")]
    public void An_oib_needs_eleven_digits_and_a_valid_check_digit(string raw)
    {
        Assert.Equal("holder.oib_invalid", Code(() => OibNumber.Normalize(raw)));
    }

    [Fact]
    public void An_oib_is_optional_and_trimmed()
    {
        Assert.Null(OibNumber.Normalize(null));
        Assert.Null(OibNumber.Normalize("  "));
        Assert.Equal(ValidOib, OibNumber.Normalize($" {ValidOib} "));
    }

    [Fact]
    public void A_reservation_holds_an_available_site_until_a_future_date()
    {
        var today = new DateOnly(2026, 11, 6);
        var site = Guid.CreateVersion7();
        var holder = Guid.CreateVersion7();

        var reservation = Reservation.Hold(Tenant, site, holder, today.AddDays(30), today, GraveSiteStatus.Available);

        Assert.True(reservation.Active);
        Assert.Equal(site, reservation.GraveSiteId);
        Assert.Equal(holder, reservation.HolderId);
        Assert.Equal(today.AddDays(30), reservation.Until);
        Assert.Equal("reservation.invalid", Code(() => Reservation.Hold(Guid.Empty, site, holder, today.AddDays(1), today, GraveSiteStatus.Available)));
        Assert.Equal("reservation.invalid", Code(() => Reservation.Hold(Tenant, Guid.Empty, holder, today.AddDays(1), today, GraveSiteStatus.Available)));
        Assert.Equal("reservation.invalid", Code(() => Reservation.Hold(Tenant, site, Guid.Empty, today.AddDays(1), today, GraveSiteStatus.Available)));
        Assert.Equal("reservation.unavailable", Code(() => Reservation.Hold(Tenant, site, holder, today.AddDays(1), today, GraveSiteStatus.Occupied)));
        Assert.Equal("right.date_invalid", Code(() => Reservation.Hold(Tenant, site, holder, today, today, GraveSiteStatus.Available)));
    }

    [Fact]
    public void A_reservation_is_cancelled_once_and_blocks_interment()
    {
        var today = new DateOnly(2026, 11, 6);
        var reservation = Reservation.Hold(Tenant, Guid.CreateVersion7(), Guid.CreateVersion7(), today.AddDays(1), today, GraveSiteStatus.Available);

        reservation.Cancel();

        Assert.False(reservation.Active);
        Assert.Equal("reservation.ended", Code(reservation.Cancel));
        Assert.Equal("reservation.blocks_interment", Code(() => BurialGuard.RefuseReserved(true)));
        BurialGuard.RefuseReserved(false);
    }

    [Fact]
    public void Shares_must_be_valid_unique_and_sum_to_the_whole()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        Assert.Equal(0.25m, RightShare.Of(first, 25).Fraction);
        Assert.Equal("right.share_invalid", Code(() => RightShare.Of(Guid.Empty, 50)));
        Assert.Equal("right.share_invalid", Code(() => RightShare.Of(first, 0)));
        Assert.Equal("right.share_invalid", Code(() => RightShare.Of(first, 101)));
        Assert.Equal("right.share_invalid", Code(() => RightShares.Require([])));
        Assert.Equal("right.share_invalid", Code(() => RightShares.Require([RightShare.Of(first, 50)])));
        Assert.Equal("right.share_invalid", Code(() => RightShares.Require([RightShare.Of(first, 50), RightShare.Of(first, 50)])));
        Assert.Equal(2, RightShares.Require([RightShare.Of(first, 50), RightShare.Of(second, 50)]).Count);
    }

    [Fact]
    public void A_right_is_granted_for_a_term_or_in_perpetuity()
    {
        var site = Guid.CreateVersion7();

        var term = GraveRight.Grant(Tenant, site, Start, 10, false, Whole(), null);
        var perpetual = GraveRight.Grant(Tenant, site, Start, null, true, Whole(), null);

        Assert.Equal(new DateOnly(2030, 1, 1), term.ValidTo);
        Assert.False(term.Perpetual);
        Assert.Equal(GraveRightStatus.Active, term.Status);
        Assert.Equal(Tenant, Assert.Single(term.Shares).TenantId);
        Assert.True(perpetual.Perpetual);
        Assert.Equal("right.term_invalid", Code(() => GraveRight.Grant(Tenant, site, Start, null, false, Whole(), null)));
        Assert.Equal("right.term_invalid", Code(() => GraveRight.Grant(Tenant, site, Start, 0, false, Whole(), null)));
        Assert.Equal("right.term_invalid", Code(() => GraveRight.Grant(Tenant, site, Start, GraveRight.MaxTermYears + 1, false, Whole(), null)));
        Assert.Equal("right.invalid", Code(() => GraveRight.Grant(Guid.Empty, site, Start, 10, false, Whole(), null)));
        Assert.Equal("right.invalid", Code(() => GraveRight.Grant(Tenant, Guid.Empty, Start, 10, false, Whole(), null)));
        Assert.Equal("right.invalid", Code(() => GraveRight.Grant(Tenant, site, default, null, true, Whole(), null)));
        Assert.Equal("right.overlap", Code(() => GraveRight.Grant(Tenant, site, Start, 10, false, Whole(), term)));
    }

    [Fact]
    public void A_new_right_may_follow_an_ended_one()
    {
        var site = Guid.CreateVersion7();
        var first = GraveRight.Grant(Tenant, site, Start, 10, false, Whole(), null);
        first.Convey(new DateOnly(2021, 1, 1), Whole());

        var next = GraveRight.Grant(Tenant, site, new DateOnly(2022, 1, 1), 5, false, Whole(), first);

        Assert.Equal(GraveRightStatus.Active, next.Status);
    }

    [Fact]
    public void A_term_right_renews_within_limits()
    {
        var right = GraveRight.Grant(Tenant, Guid.CreateVersion7(), Start, 10, false, Whole(), null);

        right.Renew(5);

        Assert.Equal(new DateOnly(2035, 1, 1), right.ValidTo);
        Assert.Equal("right.term_invalid", Code(() => right.Renew(0)));
        Assert.Equal("right.term_invalid", Code(() => right.Renew(GraveRight.MaxTermYears + 1)));

        var perpetual = GraveRight.Grant(Tenant, Guid.CreateVersion7(), Start, null, true, Whole(), null);
        Assert.Equal("right.perpetual", Code(() => perpetual.Renew(5)));

        right.Convey(new DateOnly(2021, 1, 1), Whole());
        Assert.Equal("right.ended", Code(() => right.Renew(5)));
    }

    [Fact]
    public void Conveying_a_right_ends_it_and_carries_the_remaining_term()
    {
        var right = GraveRight.Grant(Tenant, Guid.CreateVersion7(), Start, 10, false, Whole(), null);
        var on = new DateOnly(2024, 6, 1);

        var next = right.Convey(on, Whole());

        Assert.Equal(GraveRightStatus.Ended, right.Status);
        Assert.Equal(on, right.ValidTo);
        Assert.Equal(on, next.ValidFrom);
        Assert.Equal(new DateOnly(2030, 1, 1), next.ValidTo);
        Assert.Equal(right.GraveSiteId, next.GraveSiteId);
        Assert.Equal("right.ended", Code(() => right.Convey(on.AddDays(-1), Whole())));
        Assert.Equal("right.date_invalid", Code(() => right.Convey(on.AddDays(1), Whole())));
    }

    [Fact]
    public void Inheriting_a_perpetual_right_stays_perpetual()
    {
        var right = GraveRight.Grant(Tenant, Guid.CreateVersion7(), Start, null, true, Whole(), null);
        var on = new DateOnly(2024, 6, 1);

        var next = right.Inherit(on, Whole());

        Assert.True(next.Perpetual);
        Assert.Equal(on, right.ValidTo);
        Assert.Equal(GraveRightStatus.Ended, right.Status);
    }

    [Fact]
    public void Conveying_refuses_dates_outside_the_right()
    {
        var right = GraveRight.Grant(Tenant, Guid.CreateVersion7(), Start, 10, false, Whole(), null);

        Assert.Equal("right.date_invalid", Code(() => right.Convey(new DateOnly(2030, 1, 1), Whole())));
        Assert.Equal("right.date_invalid", Code(() => right.Convey(Start.AddDays(-1), Whole())));
        Assert.Equal(GraveRightStatus.Active, right.Status);
    }

    [Theory]
    [InlineData(null, DocumentKind.Other)]
    [InlineData("", DocumentKind.Other)]
    [InlineData("other", DocumentKind.Other)]
    [InlineData("contract", DocumentKind.Contract)]
    [InlineData("permit", DocumentKind.Permit)]
    [InlineData("scan", DocumentKind.Scan)]
    public void Document_kinds_round_trip_through_their_codes(string? code, DocumentKind kind)
    {
        Assert.Equal(kind, DocumentKinds.Parse(code));
        Assert.Equal(kind, DocumentKinds.Parse(DocumentKinds.ToCode(kind)));
    }

    [Fact]
    public void Unknown_document_kinds_are_refused()
    {
        Assert.Equal("document.kind_invalid", Code(() => DocumentKinds.Parse("invoice")));
        Assert.Equal("document.kind_invalid", Code(() => DocumentKinds.ToCode((DocumentKind)99)));
    }

    [Fact]
    public void Document_bytes_decode_when_the_content_matches_its_type()
    {
        var pdf = DocumentBytes.Decode(" contract.pdf ", "application/pdf", Encode("%PDF-1.7"));
        var png = DocumentBytes.Decode("plan.png", "image/png", Encode(0x89, 0x50, 0x4E, 0x47, 0x0D));
        var jpeg = DocumentBytes.Decode("photo.jpg", "image/jpeg", Encode(0xFF, 0xD8, 0xFF, 0xE0));

        Assert.Equal("contract.pdf", pdf.FileName);
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.Equal(8, pdf.Content.Length);
        Assert.Equal("image/png", png.ContentType);
        Assert.Equal("image/jpeg", jpeg.ContentType);
    }

    [Fact]
    public void Document_bytes_refuse_bad_names_types_and_content()
    {
        var pdf = Encode("%PDF-1.7");

        Assert.Equal("document.name_invalid", Code(() => DocumentBytes.Decode(null, "application/pdf", pdf)));
        Assert.Equal("document.name_invalid", Code(() => DocumentBytes.Decode(new string('a', DocumentBytes.NameMaxLength + 1), "application/pdf", pdf)));
        Assert.Equal("document.name_invalid", Code(() => DocumentBytes.Decode("a/b.pdf", "application/pdf", pdf)));
        Assert.Equal("document.name_invalid", Code(() => DocumentBytes.Decode("a\\b.pdf", "application/pdf", pdf)));
        Assert.Equal("document.type_invalid", Code(() => DocumentBytes.Decode("a.gif", "image/gif", pdf)));
        Assert.Equal("document.type_invalid", Code(() => DocumentBytes.Decode("a.pdf", null, pdf)));
        Assert.Equal("document.empty", Code(() => DocumentBytes.Decode("a.pdf", "application/pdf", " ")));
        Assert.Equal("document.type_invalid", Code(() => DocumentBytes.Decode("a.pdf", "application/pdf", "not base64!")));
        Assert.Equal("document.type_invalid", Code(() => DocumentBytes.Decode("a.pdf", "application/pdf", Encode("%PD"))));
        Assert.Equal("document.type_invalid", Code(() => DocumentBytes.Decode("a.png", "image/png", pdf)));
        Assert.Equal("document.type_invalid", Code(() => DocumentBytes.Decode("a.jpg", "image/jpeg", pdf)));
        Assert.Equal("document.type_invalid", Code(() => DocumentBytes.Decode("a.png", "image/png", Encode(0x89, 0x50))));
        Assert.Equal("document.type_invalid", Code(() => DocumentBytes.Decode("a.jpg", "image/jpeg", Encode(0xFF))));
    }

    [Fact]
    public void Document_bytes_refuse_content_over_the_limit()
    {
        var oversized = Convert.ToBase64String(new byte[DocumentBytes.MaxBytes + 1]);

        Assert.Equal("document.too_large", Code(() => DocumentBytes.Decode("a.pdf", "application/pdf", oversized)));
    }

    [Fact]
    public void A_site_document_needs_a_site_and_a_storage_key()
    {
        var site = Guid.CreateVersion7();
        var right = Guid.CreateVersion7();

        var document = SiteDocument.Store(Tenant, site, right, DocumentKind.Contract, "contract.pdf", "application/pdf", 8, "key/1");

        Assert.Equal(site, document.GraveSiteId);
        Assert.Equal(right, document.GraveRightId);
        Assert.Equal(DocumentKind.Contract, document.Kind);
        Assert.Equal("contract.pdf", document.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(8, document.ByteSize);
        Assert.Equal("key/1", document.StorageKey);
        Assert.Equal("document.invalid", Code(() => SiteDocument.Store(Guid.Empty, site, null, DocumentKind.Other, "a", "b", 1, "k")));
        Assert.Equal("document.invalid", Code(() => SiteDocument.Store(Tenant, Guid.Empty, null, DocumentKind.Other, "a", "b", 1, "k")));
        Assert.Equal("document.invalid", Code(() => SiteDocument.Store(Tenant, site, null, DocumentKind.Other, "a", "b", 1, " ")));
    }

    [Fact]
    public void A_stored_file_needs_a_tenant_and_content()
    {
        var file = StoredFile.Keep(Tenant, [1, 2, 3]);

        Assert.Equal(3, file.Content.Length);
        Assert.Equal("document.empty", Code(() => StoredFile.Keep(Guid.Empty, [1])));
        Assert.Equal("document.empty", Code(() => StoredFile.Keep(Tenant, [])));
    }

    private static IReadOnlyList<RightShare> Whole() => [RightShare.Of(Guid.CreateVersion7(), 100)];

    private static string Encode(string text) => Convert.ToBase64String(Encoding.ASCII.GetBytes(text));

    private static string Encode(params byte[] bytes) => Convert.ToBase64String(bytes);

    private static string Code(Action action) => Assert.Throws<DomainRuleException>(action).Code;

    private static string Code(Func<object?> action) => Assert.Throws<DomainRuleException>(() => action()).Code;
}

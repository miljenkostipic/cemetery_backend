using Cemetery.Domain.Identity;
using Cemetery.Domain.Operations;

namespace Cemetery.Domain.Tests;

public sealed class OperationsRulesTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly Guid Cemetery = Guid.CreateVersion7();
    private static readonly Guid Person = Guid.CreateVersion7();
    private static readonly Guid Site = Guid.CreateVersion7();
    private static readonly Guid OtherSite = Guid.CreateVersion7();

    [Fact]
    public void A_civil_time_keeps_the_clock_and_ignores_seconds()
    {
        Assert.Equal(new DateTime(2026, 6, 1, 10, 5, 0, DateTimeKind.Unspecified), CivilTime.Parse("2026-06-01T10:05:30Z", "funeral.date_invalid"));
        Assert.Equal(new DateTime(2024, 2, 29, 9, 0, 0, DateTimeKind.Unspecified), CivilTime.Parse("2024-02-29 09:00", "funeral.date_invalid"));
        Assert.Equal("2026-06-01T10:05", CivilTime.Format(CivilTime.Parse("2026-06-01T10:05", "funeral.date_invalid")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("9.10.2026. 9:05")]
    [InlineData("2026-02-31T10:00")]
    [InlineData("2026-13-01T10:00")]
    [InlineData("2026-06-01T24:00")]
    [InlineData("2026-06-01T10:60")]
    [InlineData("0000-01-01T00:00")]
    public void A_civil_time_rejects_a_clock_that_is_not_a_real_minute(string? value)
    {
        Assert.False(CivilTime.TryParse(value, out _));
        var error = Assert.Throws<DomainRuleException>(() => CivilTime.Parse(value, "funeral.date_invalid"));
        Assert.Equal("funeral.date_invalid", error.Code);
    }

    [Fact]
    public void Notes_and_names_keep_plain_text()
    {
        Assert.Equal("", OperationsText.Note(null, "work_order.notes_invalid"));
        Assert.Equal("", OperationsText.Note("   ", "work_order.notes_invalid"));
        Assert.Equal("trim", OperationsText.Note(" trim ", "work_order.notes_invalid"));
        Assert.Equal("Ana", OperationsText.Name(" Ana ", "permit.name_invalid"));
    }

    [Theory]
    [InlineData("=cmd")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("@me")]
    [InlineData("line\n")]
    [InlineData("bad\u007f")]
    public void A_formula_or_control_note_is_refused(string value)
    {
        var error = Assert.Throws<DomainRuleException>(() => OperationsText.Note(value, "work_order.notes_invalid"));
        Assert.Equal("work_order.notes_invalid", error.Code);
    }

    [Fact]
    public void A_long_note_or_a_blank_name_is_refused()
    {
        var note = Assert.Throws<DomainRuleException>(() => OperationsText.Note(new string('a', 501), "work_order.notes_invalid"));
        var blank = Assert.Throws<DomainRuleException>(() => OperationsText.Name("  ", "permit.name_invalid"));
        var longName = Assert.Throws<DomainRuleException>(() => OperationsText.Name(new string('a', 81), "permit.name_invalid"));
        var formula = Assert.Throws<DomainRuleException>(() => OperationsText.Name(" =x", "permit.name_invalid"));

        Assert.Equal("work_order.notes_invalid", note.Code);
        Assert.Equal("permit.name_invalid", blank.Code);
        Assert.Equal("permit.name_invalid", longName.Code);
        Assert.Equal("permit.name_invalid", formula.Code);
    }

    [Fact]
    public void A_funeral_keeps_its_place_and_can_be_cancelled_once()
    {
        var funeral = Funeral.Schedule(Tenant, Cemetery, Person, Site, "2026-06-01T10:00", "2026-06-01T11:00", "chapel");

        Assert.Equal(FuneralPlace.Chapel, funeral.Place);
        Assert.Equal(FuneralStatus.Scheduled, funeral.Status);
        funeral.Cancel();
        funeral.Cancel();

        Assert.Equal(FuneralStatus.Cancelled, funeral.Status);
        Assert.Equal("chapel", FuneralPlaces.ToCode(FuneralPlace.Chapel));
        Assert.Equal("mortuary", FuneralPlaces.ToCode(FuneralPlace.Mortuary));
        Assert.Equal("graveside", FuneralPlaces.ToCode(FuneralPlace.Graveside));
        Assert.Equal(FuneralPlace.Graveside, FuneralPlaces.Parse("graveside"));
        Assert.Equal("scheduled", FuneralStatuses.ToCode(FuneralStatus.Scheduled));
        Assert.Equal("cancelled", FuneralStatuses.ToCode(FuneralStatus.Cancelled));
        Assert.Equal(FuneralStatus.Cancelled, FuneralStatuses.Parse("cancelled"));
    }

    [Fact]
    public void Unknown_funeral_codes_are_refused()
    {
        Assert.Equal("funeral.place_invalid", Assert.Throws<DomainRuleException>(() => FuneralPlaces.Parse("paint")).Code);
        Assert.Equal("funeral.date_invalid", Assert.Throws<DomainRuleException>(() => FuneralStatuses.Parse("nowhere")).Code);
    }

    [Fact]
    public void Cast_funeral_codes_are_refused()
    {
        var place = Assert.Throws<DomainRuleException>(() => FuneralPlaces.ToCode((FuneralPlace)9));
        var status = Assert.Throws<DomainRuleException>(() => FuneralStatuses.ToCode((FuneralStatus)9));

        Assert.Equal("funeral.place_invalid", place.Code);
        Assert.Equal("funeral.date_invalid", status.Code);
    }

    [Theory]
    [InlineData("2026-06-01T11:00", "2026-06-01T10:00")]
    [InlineData("2026-06-01T10:00", "2026-06-01T10:00")]
    [InlineData("not-a-time", "2026-06-01T11:00")]
    public void A_funeral_needs_a_later_end(string start, string end)
    {
        var error = Assert.Throws<DomainRuleException>(() => Funeral.Schedule(Tenant, Cemetery, Person, Site, start, end, "chapel"));
        Assert.Equal("funeral.date_invalid", error.Code);
    }

    [Fact]
    public void A_funeral_needs_its_parties()
    {
        Assert.Equal("tenant.required", Assert.Throws<DomainRuleException>(() => Funeral.Schedule(Guid.Empty, Cemetery, Person, Site, "2026-06-01T10:00", "2026-06-01T11:00", "chapel")).Code);
        Assert.Equal("cemetery.not_found", Assert.Throws<DomainRuleException>(() => Funeral.Schedule(Tenant, Guid.Empty, Person, Site, "2026-06-01T10:00", "2026-06-01T11:00", "chapel")).Code);
        Assert.Equal("funeral.deceased_invalid", Assert.Throws<DomainRuleException>(() => Funeral.Schedule(Tenant, Cemetery, Guid.Empty, Site, "2026-06-01T10:00", "2026-06-01T11:00", "chapel")).Code);
        Assert.Equal("funeral.site_invalid", Assert.Throws<DomainRuleException>(() => Funeral.Schedule(Tenant, Cemetery, Person, Guid.Empty, "2026-06-01T10:00", "2026-06-01T11:00", "chapel")).Code);
    }

    [Fact]
    public void Overlapping_funerals_clash_on_the_same_grave_or_the_same_room()
    {
        var chapel = Funeral.Schedule(Tenant, Cemetery, Person, Site, "2026-06-01T10:00", "2026-06-01T11:00", "chapel");
        var sameGrave = Funeral.Schedule(Tenant, Cemetery, Person, Site, "2026-06-01T10:30", "2026-06-01T11:30", "graveside");
        var sameRoom = Funeral.Schedule(Tenant, Cemetery, Person, OtherSite, "2026-06-01T10:30", "2026-06-01T11:30", "chapel");
        var otherRoom = Funeral.Schedule(Tenant, Cemetery, Person, OtherSite, "2026-06-01T10:30", "2026-06-01T11:30", "mortuary");
        var later = Funeral.Schedule(Tenant, Cemetery, Person, Site, "2026-06-01T11:00", "2026-06-01T12:00", "chapel");
        var graveside = Funeral.Schedule(Tenant, Cemetery, Person, Guid.CreateVersion7(), "2026-06-01T10:30", "2026-06-01T11:30", "graveside");

        Assert.True(chapel.Clashes(sameGrave));
        Assert.True(chapel.Clashes(sameRoom));
        Assert.False(chapel.Clashes(otherRoom));
        Assert.False(chapel.Clashes(later));
        Assert.False(graveside.Clashes(otherRoom));
        chapel.Cancel();
        Assert.False(chapel.Clashes(sameRoom));
    }

    [Fact]
    public void A_room_booking_clashes_with_another_booking_or_a_funeral_in_that_room()
    {
        var booking = ChapelBooking.Book(Tenant, Cemetery, "chapel", "2026-06-01T10:00", "2026-06-01T11:00");
        var overlap = ChapelBooking.Book(Tenant, Cemetery, "chapel", "2026-06-01T10:30", "2026-06-01T11:30");
        var mortuary = ChapelBooking.Book(Tenant, Cemetery, "mortuary", "2026-06-01T10:30", "2026-06-01T11:30");
        var funeral = Funeral.Schedule(Tenant, Cemetery, Person, Site, "2026-06-01T10:15", "2026-06-01T10:45", "chapel");
        var graveside = Funeral.Schedule(Tenant, Cemetery, Person, Site, "2026-06-01T10:15", "2026-06-01T10:45", "graveside");

        Assert.True(booking.Clashes(overlap));
        Assert.False(booking.Clashes(mortuary));
        Assert.True(booking.Clashes(funeral));
        Assert.True(funeral.Clashes(booking));
        Assert.False(funeral.Clashes(mortuary));
        Assert.False(graveside.Clashes(booking));
        Assert.False(booking.Clashes(graveside));
        funeral.Cancel();
        Assert.False(booking.Clashes(funeral));
        Assert.Equal("chapel", ChapelPlaces.ToCode(ChapelPlace.Chapel));
        Assert.Equal("mortuary", ChapelPlaces.ToCode(ChapelPlace.Mortuary));
        Assert.Equal(ChapelPlace.Mortuary, ChapelPlaces.Parse("mortuary"));
    }

    [Fact]
    public void A_chapel_booking_needs_a_room_and_a_range()
    {
        Assert.Equal("chapel.place_invalid", Assert.Throws<DomainRuleException>(() => ChapelBooking.Book(Tenant, Cemetery, "graveside", "2026-06-01T10:00", "2026-06-01T11:00")).Code);
        Assert.Equal("chapel.place_invalid", Assert.Throws<DomainRuleException>(() => ChapelPlaces.ToCode((ChapelPlace)9)).Code);
        Assert.Equal("chapel.date_invalid", Assert.Throws<DomainRuleException>(() => ChapelBooking.Book(Tenant, Cemetery, "chapel", "2026-06-01T11:00", "2026-06-01T10:00")).Code);
        Assert.Equal("tenant.required", Assert.Throws<DomainRuleException>(() => ChapelBooking.Book(Guid.Empty, Cemetery, "chapel", "2026-06-01T10:00", "2026-06-01T11:00")).Code);
        Assert.Equal("cemetery.not_found", Assert.Throws<DomainRuleException>(() => ChapelBooking.Book(Tenant, Guid.Empty, "chapel", "2026-06-01T10:00", "2026-06-01T11:00")).Code);
        Assert.Equal("funeral.date_invalid", Assert.Throws<DomainRuleException>(() => ScheduleWindow.RequireOrdered(DateTime.UnixEpoch, DateTime.UnixEpoch, "funeral.date_invalid")).Code);
    }

    [Fact]
    public void A_work_order_opens_and_completes_once()
    {
        var order = WorkOrder.Open(Tenant, Cemetery, Site, "digging", new DateOnly(2026, 6, 1), " dig ");

        Assert.Equal(WorkKind.Digging, order.Kind);
        Assert.Equal("dig", order.Notes);
        Assert.Equal(WorkOrderStatus.Open, order.Status);
        order.Complete(new DateOnly(2026, 6, 2), " done ");

        Assert.Equal(WorkOrderStatus.Completed, order.Status);
        Assert.Equal(new DateOnly(2026, 6, 2), order.CompletedOn);
        Assert.Equal("done", order.CompletionNote);
        Assert.Equal("dig", order.Notes);
        Assert.Equal("work_order.closed", Assert.Throws<DomainRuleException>(() => order.Complete(new DateOnly(2026, 6, 3), "again")).Code);
    }

    [Fact]
    public void Work_order_codes_and_parties_are_checked()
    {
        Assert.Equal("digging", WorkKinds.ToCode(WorkKind.Digging));
        Assert.Equal("monument", WorkKinds.ToCode(WorkKind.Monument));
        Assert.Equal("maintenance", WorkKinds.ToCode(WorkKind.Maintenance));
        Assert.Equal(WorkKind.Maintenance, WorkKinds.Parse("maintenance"));
        Assert.Equal("open", WorkOrderStatuses.ToCode(WorkOrderStatus.Open));
        Assert.Equal("completed", WorkOrderStatuses.ToCode(WorkOrderStatus.Completed));
        Assert.Equal(WorkOrderStatus.Completed, WorkOrderStatuses.Parse("completed"));
        Assert.Equal("work_order.kind_invalid", Assert.Throws<DomainRuleException>(() => WorkKinds.Parse("paint")).Code);
        Assert.Equal("work_order.kind_invalid", Assert.Throws<DomainRuleException>(() => WorkKinds.ToCode((WorkKind)9)).Code);
        Assert.Equal("work_order.kind_invalid", Assert.Throws<DomainRuleException>(() => WorkOrderStatuses.Parse("shut")).Code);
        Assert.Equal("work_order.kind_invalid", Assert.Throws<DomainRuleException>(() => WorkOrderStatuses.ToCode((WorkOrderStatus)9)).Code);
        Assert.Equal("work_order.date_invalid", Assert.Throws<DomainRuleException>(() => WorkOrder.Open(Tenant, Cemetery, Site, "digging", default, "")).Code);
        Assert.Equal("tenant.required", Assert.Throws<DomainRuleException>(() => WorkOrder.Open(Guid.Empty, Cemetery, Site, "digging", new DateOnly(2026, 6, 1), "")).Code);
        Assert.Equal("cemetery.not_found", Assert.Throws<DomainRuleException>(() => WorkOrder.Open(Tenant, Guid.Empty, Site, "digging", new DateOnly(2026, 6, 1), "")).Code);
        Assert.Equal("work_order.site_invalid", Assert.Throws<DomainRuleException>(() => WorkOrder.Open(Tenant, Cemetery, Guid.Empty, "digging", new DateOnly(2026, 6, 1), "")).Code);
        Assert.Equal("", WorkOrder.Open(Tenant, Cemetery, Site, "maintenance", new DateOnly(2026, 6, 1), null).Notes);
    }

    [Fact]
    public void Only_a_monument_order_can_carry_a_permit_and_completion_needs_a_date()
    {
        var digging = WorkOrder.Open(Tenant, Cemetery, Site, "digging", new DateOnly(2026, 6, 1), "");
        var monument = WorkOrder.Open(Tenant, Cemetery, Site, "monument", new DateOnly(2026, 6, 1), "");
        var permitId = Guid.CreateVersion7();

        Assert.Equal("work_order.kind_invalid", Assert.Throws<DomainRuleException>(() => digging.UsePermit(permitId)).Code);
        Assert.Equal("permit.site_invalid", Assert.Throws<DomainRuleException>(() => monument.UsePermit(Guid.Empty)).Code);
        monument.UsePermit(permitId);
        Assert.Equal(permitId, monument.PermitId);
        monument.ClearPermit();
        Assert.Null(monument.PermitId);
        Assert.Equal("completion.date_invalid", Assert.Throws<DomainRuleException>(() => digging.Complete(default, "")).Code);
        Assert.Equal("completion.note_invalid", Assert.Throws<DomainRuleException>(() => digging.Complete(new DateOnly(2026, 6, 2), "=no")).Code);
    }

    [Fact]
    public void A_permit_covers_its_site_until_it_expires_and_the_latest_one_wins()
    {
        var day = new DateOnly(2026, 6, 1);
        var early = MonumentPermit.Issue(Tenant, Cemetery, Site, "Ana", new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1));
        var later = MonumentPermit.Issue(Tenant, Cemetery, Site, "Ana", new DateOnly(2026, 5, 1), new DateOnly(2026, 12, 1));
        var other = MonumentPermit.Issue(Tenant, Cemetery, OtherSite, "Ivo", day, day);
        var sameDayA = MonumentPermit.Issue(Tenant, Cemetery, Site, "Ana", day, new DateOnly(2026, 12, 1));
        var sameDayB = MonumentPermit.Issue(Tenant, Cemetery, Site, "Ana", day, new DateOnly(2026, 12, 1));

        Assert.True(early.Covers(Site, day));
        Assert.True(early.Covers(Site, early.IssuedOn));
        Assert.False(early.Covers(Site, day.AddDays(1)));
        Assert.False(early.Covers(OtherSite, day));
        Assert.Equal("active", early.StatusOn(day));
        Assert.Equal("expired", early.StatusOn(day.AddDays(1)));
        Assert.Equal(later.Id, MonumentPermit.BestCover([early, other, later], Site, day)!.Id);
        Assert.Equal(
            sameDayA.Id.CompareTo(sameDayB.Id) > 0 ? sameDayA.Id : sameDayB.Id,
            MonumentPermit.BestCover([sameDayA, sameDayB], Site, day)!.Id);
        Assert.Equal(
            MonumentPermit.BestCover([sameDayA, sameDayB], Site, day)!.Id,
            MonumentPermit.BestCover([sameDayB, sameDayA], Site, day)!.Id);
        Assert.Null(MonumentPermit.BestCover([other], Site, day));
    }

    [Fact]
    public void A_permit_needs_a_holder_and_an_ordered_period()
    {
        Assert.Equal("permit.date_invalid", Assert.Throws<DomainRuleException>(() => MonumentPermit.Issue(Tenant, Cemetery, Site, "Ana", new DateOnly(2026, 6, 2), new DateOnly(2026, 6, 1))).Code);
        Assert.Equal("permit.date_invalid", Assert.Throws<DomainRuleException>(() => MonumentPermit.Issue(Tenant, Cemetery, Site, "Ana", default, new DateOnly(2026, 6, 1))).Code);
        Assert.Equal("permit.name_invalid", Assert.Throws<DomainRuleException>(() => MonumentPermit.Issue(Tenant, Cemetery, Site, " ", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2))).Code);
        Assert.Equal("tenant.required", Assert.Throws<DomainRuleException>(() => MonumentPermit.Issue(Guid.Empty, Cemetery, Site, "Ana", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2))).Code);
        Assert.Equal("cemetery.not_found", Assert.Throws<DomainRuleException>(() => MonumentPermit.Issue(Tenant, Guid.Empty, Site, "Ana", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2))).Code);
        Assert.Equal("permit.site_invalid", Assert.Throws<DomainRuleException>(() => MonumentPermit.Issue(Tenant, Cemetery, Guid.Empty, "Ana", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 2))).Code);
        Assert.Equal("Ana", MonumentPermit.Issue(Tenant, Cemetery, Site, "Ana", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 1)).HolderName);
    }

    [Fact]
    public void A_photo_accepts_jpeg_and_png_bytes()
    {
        var jpeg = WorkOrderPhoto.Record(Tenant, Guid.CreateVersion7(), "image/jpeg", Convert.ToBase64String([0xFF, 0xD8, 0xFF, 0x00]));
        var png = PhotoBytes.Decode("image/png", Convert.ToBase64String([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]));

        Assert.Equal("image/jpeg", jpeg.ContentType);
        Assert.Equal(4, jpeg.Content.Length);
        Assert.Equal("image/png", png.ContentType);
    }

    [Theory]
    [InlineData("image/gif", "aaaa")]
    [InlineData("image/jpeg", "")]
    [InlineData("image/jpeg", "   ")]
    [InlineData("image/jpeg", "****")]
    [InlineData("image/jpeg", "AA==")]
    [InlineData("image/png", "/9g/")]
    public void A_photo_rejects_the_wrong_bytes(string? contentType, string encoded)
    {
        var error = Assert.Throws<DomainRuleException>(() => PhotoBytes.Decode(contentType, encoded));
        Assert.True(error.Code is "photo.type_invalid" or "photo.empty");
    }

    [Fact]
    public void A_photo_rejects_a_huge_payload_and_a_missing_order()
    {
        var huge = Assert.Throws<DomainRuleException>(() => PhotoBytes.Decode("image/jpeg", new string('A', 6_666_669)));
        var over = Assert.Throws<DomainRuleException>(() => PhotoBytes.Decode("image/jpeg", new string('A', 6_666_668)));
        var missing = Assert.Throws<DomainRuleException>(() => WorkOrderPhoto.Record(Guid.Empty, Guid.CreateVersion7(), "image/jpeg", "AA=="));
        var order = Assert.Throws<DomainRuleException>(() => WorkOrderPhoto.Record(Tenant, Guid.Empty, "image/jpeg", "AA=="));

        Assert.Equal("photo.too_large", huge.Code);
        Assert.Equal("photo.too_large", over.Code);
        Assert.Equal("work_order.not_found", missing.Code);
        Assert.Equal("work_order.not_found", order.Code);
    }
}

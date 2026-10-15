using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Operations;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Operations;
using Cemetery.Domain.Register;
using FluentValidation;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Application.Tests;

public sealed class OperationsHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_clerk_schedules_lists_and_cancels_a_funeral()
    {
        var world = new World();
        var scheduled = await Schedule(world).Handle(Command(world, "2026-06-01T10:00", "2026-06-01T11:00"), CancellationToken.None);
        var stored = Scheduled(world);
        world.Store.ListFuneralsAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns([stored]);
        world.Store.FindFuneralAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);
        var listed = await new ListFuneralsHandler(world.User, world.Tenant, world.Memberships, world.Layouts, world.Register, world.Store)
            .Handle(new ListFunerals(world.Cemetery.Id), CancellationToken.None);
        var cancelled = await new CancelFuneralHandler(world.User, world.Tenant, world.Memberships, world.Layouts, world.Register, world.Store, world.Unit)
            .Handle(new CancelFuneral(stored.Id), CancellationToken.None);

        Assert.Equal("Ana Horvat", scheduled.DeceasedName);
        Assert.Equal("A-1", scheduled.GraveSiteCode);
        Assert.Equal("scheduled", scheduled.Status);
        Assert.Equal("Ana Horvat", listed[0].DeceasedName);
        Assert.Equal("cancelled", cancelled.Status);
        await world.Store.Received(1).AddFuneralAsync(Arg.Any<Funeral>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_funeral_refuses_a_missing_cemetery_person_or_site()
    {
        var world = new World();
        world.Layouts.FindCemeteryAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns((CemeteryPlace?)null);
        var missingCemetery = await Assert.ThrowsAsync<NotFoundException>(() => Schedule(world).Handle(Command(world, "2026-06-01T10:00", "2026-06-01T11:00"), CancellationToken.None));
        world.Layouts.FindCemeteryAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns(world.Cemetery);
        world.Register.FindDeceasedAsync(world.Person.Id, Arg.Any<CancellationToken>()).Returns((Deceased?)null);
        var missingPerson = await Assert.ThrowsAsync<DomainRuleException>(() => Schedule(world).Handle(Command(world, "2026-06-01T10:00", "2026-06-01T11:00"), CancellationToken.None));
        var removedPerson = Deceased.Record(world.TenantId, world.Cemetery.Id, "Ana", "Horvat", null, null, Now);
        removedPerson.Remove(Now);
        world.Register.FindDeceasedAsync(world.Person.Id, Arg.Any<CancellationToken>()).Returns(removedPerson);
        var removed = await Assert.ThrowsAsync<DomainRuleException>(() => Schedule(world).Handle(Command(world, "2026-06-01T10:00", "2026-06-01T11:00"), CancellationToken.None));
        var elsewhere = Deceased.Record(world.TenantId, Guid.CreateVersion7(), "Ivo", "Ivic", null, null, Now);
        world.Register.FindDeceasedAsync(world.Person.Id, Arg.Any<CancellationToken>()).Returns(elsewhere);
        var otherCemetery = await Assert.ThrowsAsync<DomainRuleException>(() => Schedule(world).Handle(Command(world, "2026-06-01T10:00", "2026-06-01T11:00"), CancellationToken.None));
        world.Register.FindDeceasedAsync(world.Person.Id, Arg.Any<CancellationToken>()).Returns(world.Person);
        world.Layouts.FindGraveSiteAsync(world.Site.Id, Arg.Any<CancellationToken>()).Returns((GraveSite?)null);
        var missingSite = await Assert.ThrowsAsync<DomainRuleException>(() => Schedule(world).Handle(Command(world, "2026-06-01T10:00", "2026-06-01T11:00"), CancellationToken.None));

        Assert.Equal("cemetery.not_found", missingCemetery.Code);
        Assert.Equal("funeral.deceased_invalid", missingPerson.Code);
        Assert.Equal("funeral.deceased_invalid", removed.Code);
        Assert.Equal("funeral.deceased_invalid", otherCemetery.Code);
        Assert.Equal("funeral.site_invalid", missingSite.Code);
    }

    [Fact]
    public async Task A_funeral_refuses_a_site_from_another_cemetery_and_a_clash()
    {
        var world = new World();
        var other = GraveSite.Place(world.TenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), null, "B-1", GraveSiteKind.SingleGrave, Box());
        world.Layouts.FindGraveSiteAsync(world.Site.Id, Arg.Any<CancellationToken>()).Returns(other);
        var wrongSite = await Assert.ThrowsAsync<DomainRuleException>(() => Schedule(world).Handle(Command(world, "2026-06-01T10:00", "2026-06-01T11:00"), CancellationToken.None));
        world.Layouts.FindGraveSiteAsync(world.Site.Id, Arg.Any<CancellationToken>()).Returns(world.Site);
        var existing = Funeral.Schedule(world.TenantId, world.Cemetery.Id, world.Person.Id, world.Site.Id, "2026-06-01T10:00", "2026-06-01T11:00", "chapel");
        world.Store.ListFuneralsAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns([existing]);
        var funeralClash = await Assert.ThrowsAsync<ConflictException>(() => Schedule(world).Handle(Command(world, "2026-06-01T10:30", "2026-06-01T11:30"), CancellationToken.None));
        world.Store.ListFuneralsAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns([Later(world)]);
        var booking = ChapelBooking.Book(world.TenantId, world.Cemetery.Id, "chapel", "2026-06-01T10:00", "2026-06-01T11:00");
        world.Store.ListBookingsAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns([booking]);
        var bookingClash = await Assert.ThrowsAsync<ConflictException>(() => Schedule(world).Handle(Command(world, "2026-06-01T10:30", "2026-06-01T11:30"), CancellationToken.None));

        Assert.Equal("funeral.site_invalid", wrongSite.Code);
        Assert.Equal("funeral.conflict", funeralClash.Code);
        Assert.Equal("funeral.conflict", bookingClash.Code);
    }

    [Fact]
    public async Task A_field_worker_cannot_schedule_and_a_missing_funeral_cannot_be_cancelled()
    {
        var world = new World();
        world.Memberships.RoleInCurrentTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(MembershipRole.FieldWorker);
        var forbidden = await Assert.ThrowsAsync<ForbiddenException>(() => Schedule(world).Handle(Command(world, "2026-06-01T10:00", "2026-06-01T11:00"), CancellationToken.None));
        world.Memberships.RoleInCurrentTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(MembershipRole.CemeteryClerk);
        var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
            new CancelFuneralHandler(world.User, world.Tenant, world.Memberships, world.Layouts, world.Register, world.Store, world.Unit)
                .Handle(new CancelFuneral(Guid.CreateVersion7()), CancellationToken.None));
        world.Store.ListFuneralsAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<Funeral>());
        world.Register.ListDeceasedAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<Deceased>());
        world.Layouts.ListByCemeteryAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraveSite>());
        var listed = await new ListFuneralsHandler(world.User, world.Tenant, world.Memberships, world.Layouts, world.Register, world.Store)
            .Handle(new ListFunerals(world.Cemetery.Id), CancellationToken.None);

        Assert.Equal("register.forbidden", forbidden.Code);
        Assert.Equal("funeral.not_found", missing.Code);
        Assert.Empty(listed);
    }

    [Fact]
    public async Task A_clerk_books_a_chapel_unless_the_room_is_taken()
    {
        var world = new World();
        var booked = await Book(world).Handle(new BookChapel(world.Cemetery.Id, "mortuary", "2026-06-01T12:00", "2026-06-01T13:00"), CancellationToken.None);
        var existing = ChapelBooking.Book(world.TenantId, world.Cemetery.Id, "chapel", "2026-06-01T10:00", "2026-06-01T11:00");
        world.Store.ListBookingsAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns([existing]);
        var bookingClash = await Assert.ThrowsAsync<ConflictException>(() => Book(world).Handle(new BookChapel(world.Cemetery.Id, "chapel", "2026-06-01T10:30", "2026-06-01T11:30"), CancellationToken.None));
        world.Store.ListBookingsAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns([ChapelBooking.Book(world.TenantId, world.Cemetery.Id, "mortuary", "2026-06-01T15:00", "2026-06-01T16:00")]);
        var funeral = Funeral.Schedule(world.TenantId, world.Cemetery.Id, world.Person.Id, world.Site.Id, "2026-06-01T10:00", "2026-06-01T11:00", "chapel");
        world.Store.ListFuneralsAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns([funeral]);
        var funeralClash = await Assert.ThrowsAsync<ConflictException>(() => Book(world).Handle(new BookChapel(world.Cemetery.Id, "chapel", "2026-06-01T10:30", "2026-06-01T11:30"), CancellationToken.None));
        var listed = await new ListChapelBookingsHandler(world.User, world.Tenant, world.Memberships, world.Store)
            .Handle(new ListChapelBookings(world.Cemetery.Id), CancellationToken.None);

        Assert.Equal("mortuary", booked.Place);
        Assert.Equal("chapel.conflict", bookingClash.Code);
        Assert.Equal("chapel.conflict", funeralClash.Code);
        Assert.Equal("mortuary", listed[0].Place);
    }

    [Fact]
    public async Task A_monument_order_keeps_the_covering_permit()
    {
        var world = new World();
        var permit = MonumentPermit.Issue(world.TenantId, world.Cemetery.Id, world.Site.Id, "Ana", new DateOnly(2026, 6, 1), new DateOnly(2026, 12, 1));
        world.Store.ListPermitsAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns([permit]);
        var monument = await Open(world).Handle(new OpenWorkOrder(world.Cemetery.Id, world.Site.Id, "monument", new DateOnly(2026, 6, 1), "stone"), CancellationToken.None);
        world.Store.ListPermitsAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<MonumentPermit>());
        var bare = await Open(world).Handle(new OpenWorkOrder(world.Cemetery.Id, world.Site.Id, "monument", new DateOnly(2026, 6, 2), ""), CancellationToken.None);
        var digging = await Open(world).Handle(new OpenWorkOrder(world.Cemetery.Id, world.Site.Id, "digging", new DateOnly(2026, 6, 3), ""), CancellationToken.None);

        Assert.Equal(permit.Id, monument.PermitId);
        Assert.Null(bare.PermitId);
        Assert.Null(digging.PermitId);
        Assert.Equal("A-1", digging.GraveSiteCode);
    }

    [Fact]
    public async Task Orders_and_permits_list_even_when_the_site_is_gone()
    {
        var world = new World();
        var order = WorkOrder.Open(world.TenantId, world.Cemetery.Id, Guid.CreateVersion7(), "maintenance", new DateOnly(2026, 6, 1), "trim");
        var permit = MonumentPermit.Issue(world.TenantId, world.Cemetery.Id, Guid.CreateVersion7(), "Ana", new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 2));
        world.Store.ListWorkOrdersAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns([order]);
        world.Store.ListPermitsAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns([permit]);
        world.Layouts.ListByCemeteryAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraveSite>());
        var orders = await new ListWorkOrdersHandler(world.User, world.Tenant, world.Memberships, world.Layouts, world.Store)
            .Handle(new ListWorkOrders(world.Cemetery.Id), CancellationToken.None);
        var permits = await new ListPermitsHandler(world.User, world.Tenant, world.Memberships, world.Layouts, world.Store, Clock())
            .Handle(new ListPermits(world.Cemetery.Id), CancellationToken.None);

        Assert.Equal("", orders[0].GraveSiteCode);
        Assert.Equal("expired", permits[0].Status);
        Assert.Equal("", permits[0].GraveSiteCode);
    }

    [Fact]
    public async Task A_field_worker_completes_today_and_can_add_a_photo_afterward()
    {
        var world = new World();
        world.Memberships.RoleInCurrentTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(MembershipRole.FieldWorker);
        var order = WorkOrder.Open(world.TenantId, world.Cemetery.Id, world.Site.Id, "digging", new DateOnly(2026, 6, 1), "");
        world.Store.ListOpenOnAsync(new DateOnly(2026, 6, 1), Arg.Any<CancellationToken>()).Returns([order]);
        world.Store.FindWorkOrderAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var tasks = await new ListFieldTasksHandler(world.User, world.Tenant, world.Memberships, world.Layouts, world.Store, Clock())
            .Handle(new ListFieldTasks(), CancellationToken.None);
        var completed = await new CompleteWorkOrderHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
            .Handle(new CompleteWorkOrder(order.Id, new DateOnly(2026, 6, 1), "done"), CancellationToken.None);
        var photo = await new AttachWorkOrderPhotoHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
            .Handle(new AttachWorkOrderPhoto(order.Id, "image/jpeg", Convert.ToBase64String([0xFF, 0xD8, 0xFF])), CancellationToken.None);

        Assert.Equal(world.Cemetery.Name, tasks[0].CemeteryName);
        Assert.Equal("A-1", tasks[0].GraveSiteCode);
        Assert.NotNull(tasks[0].Location);
        Assert.Equal("completed", completed.Status);
        Assert.Equal("image/jpeg", photo.ContentType);
    }

    [Fact]
    public async Task Field_work_hides_a_task_without_a_cemetery_and_refuses_strangers()
    {
        var world = new World();
        var order = WorkOrder.Open(world.TenantId, world.Cemetery.Id, Guid.CreateVersion7(), "digging", new DateOnly(2026, 6, 1), "");
        world.Store.ListOpenOnAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([order]);
        world.Layouts.ListCemeteriesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<CemeteryPlace>());
        var tasks = await new ListFieldTasksHandler(world.User, world.Tenant, world.Memberships, world.Layouts, world.Store, Clock())
            .Handle(new ListFieldTasks(), CancellationToken.None);
        world.Memberships.RoleInCurrentTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(MembershipRole.GraveHolder);
        var forbidden = await Assert.ThrowsAsync<ForbiddenException>(() =>
            new ListFieldTasksHandler(world.User, world.Tenant, world.Memberships, world.Layouts, world.Store, Clock())
                .Handle(new ListFieldTasks(), CancellationToken.None));
        world.User.UserId.Returns((Guid?)null);
        var anonymous = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            new CompleteWorkOrderHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
                .Handle(new CompleteWorkOrder(order.Id, new DateOnly(2026, 6, 1), ""), CancellationToken.None));
        world.User.UserId.Returns(Guid.CreateVersion7());
        world.Tenant.OrganizationId.Returns((Guid?)null);
        var noTenant = await Assert.ThrowsAsync<ForbiddenException>(() =>
            new AttachWorkOrderPhotoHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
                .Handle(new AttachWorkOrderPhoto(order.Id, "image/jpeg", "AA=="), CancellationToken.None));

        Assert.Equal("", tasks[0].CemeteryName);
        Assert.Null(tasks[0].Location);
        Assert.Equal("register.forbidden", forbidden.Code);
        Assert.Equal("auth.required", anonymous.Code);
        Assert.Equal("tenant.required", noTenant.Code);
    }

    [Fact]
    public async Task Completing_a_missing_or_closed_order_is_refused()
    {
        var world = new World();
        var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
            new CompleteWorkOrderHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
                .Handle(new CompleteWorkOrder(Guid.CreateVersion7(), new DateOnly(2026, 6, 1), ""), CancellationToken.None));
        var order = WorkOrder.Open(world.TenantId, world.Cemetery.Id, world.Site.Id, "digging", new DateOnly(2026, 6, 1), "");
        order.Complete(new DateOnly(2026, 6, 1), "");
        world.Store.FindWorkOrderAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var closed = await Assert.ThrowsAsync<DomainRuleException>(() =>
            new CompleteWorkOrderHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
                .Handle(new CompleteWorkOrder(order.Id, new DateOnly(2026, 6, 1), ""), CancellationToken.None));
        var photo = await Assert.ThrowsAsync<NotFoundException>(() =>
            new AttachWorkOrderPhotoHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
                .Handle(new AttachWorkOrderPhoto(Guid.CreateVersion7(), "image/jpeg", "AA=="), CancellationToken.None));

        Assert.Equal("work_order.not_found", missing.Code);
        Assert.Equal("work_order.closed", closed.Code);
        Assert.Equal("work_order.not_found", photo.Code);
    }

    [Fact]
    public async Task A_missing_cemetery_blocks_a_booking_an_order_and_a_permit()
    {
        var world = new World();
        world.Layouts.FindCemeteryAsync(world.Cemetery.Id, Arg.Any<CancellationToken>()).Returns((CemeteryPlace?)null);
        var booking = await Assert.ThrowsAsync<NotFoundException>(() => Book(world).Handle(new BookChapel(world.Cemetery.Id, "chapel", "2026-06-01T10:00", "2026-06-01T11:00"), CancellationToken.None));
        var order = await Assert.ThrowsAsync<NotFoundException>(() => Open(world).Handle(new OpenWorkOrder(world.Cemetery.Id, world.Site.Id, "digging", new DateOnly(2026, 6, 1), ""), CancellationToken.None));
        var permit = await Assert.ThrowsAsync<NotFoundException>(() => Issue(world).Handle(new IssuePermit(world.Cemetery.Id, world.Site.Id, "Ana", new DateOnly(2026, 6, 1), new DateOnly(2026, 12, 1)), CancellationToken.None));

        Assert.Equal("cemetery.not_found", booking.Code);
        Assert.Equal("cemetery.not_found", order.Code);
        Assert.Equal("cemetery.not_found", permit.Code);
    }

    [Fact]
    public async Task A_permit_is_issued_for_a_site_in_the_cemetery()
    {
        var world = new World();
        var issued = await Issue(world).Handle(new IssuePermit(world.Cemetery.Id, world.Site.Id, "Ana", new DateOnly(2026, 6, 1), new DateOnly(2026, 12, 1)), CancellationToken.None);
        world.Layouts.FindGraveSiteAsync(world.Site.Id, Arg.Any<CancellationToken>()).Returns((GraveSite?)null);
        var missing = await Assert.ThrowsAsync<DomainRuleException>(() => Open(world).Handle(new OpenWorkOrder(world.Cemetery.Id, world.Site.Id, "digging", new DateOnly(2026, 6, 1), ""), CancellationToken.None));

        Assert.Equal("active", issued.Status);
        Assert.Equal("work_order.site_invalid", missing.Code);
    }

    [Fact]
    public void Validators_reject_empty_commands_and_accept_a_complete_one()
    {
        var id = Guid.CreateVersion7();
        var day = new DateOnly(2026, 6, 1);
        Assert.False(new ScheduleFuneralValidator().Validate(new ScheduleFuneral(Guid.Empty, Guid.Empty, Guid.Empty, "", "", "")).IsValid);
        Assert.True(new ScheduleFuneralValidator().Validate(new ScheduleFuneral(id, id, id, "2026-06-01T10:00", "2026-06-01T11:00", "chapel")).IsValid);
        Assert.False(new CancelFuneralValidator().Validate(new CancelFuneral(Guid.Empty)).IsValid);
        Assert.True(new CancelFuneralValidator().Validate(new CancelFuneral(id)).IsValid);
        Assert.False(new BookChapelValidator().Validate(new BookChapel(Guid.Empty, "", "", "")).IsValid);
        Assert.True(new BookChapelValidator().Validate(new BookChapel(id, "chapel", "2026-06-01T10:00", "2026-06-01T11:00")).IsValid);
        Assert.False(new OpenWorkOrderValidator().Validate(new OpenWorkOrder(Guid.Empty, Guid.Empty, "", default, "")).IsValid);
        Assert.True(new OpenWorkOrderValidator().Validate(new OpenWorkOrder(id, id, "digging", day, "")).IsValid);
        Assert.False(new IssuePermitValidator().Validate(new IssuePermit(Guid.Empty, Guid.Empty, "", default, default)).IsValid);
        Assert.True(new IssuePermitValidator().Validate(new IssuePermit(id, id, "Ana", day, day)).IsValid);
        Assert.False(new CompleteWorkOrderValidator().Validate(new CompleteWorkOrder(Guid.Empty, default, "")).IsValid);
        Assert.True(new CompleteWorkOrderValidator().Validate(new CompleteWorkOrder(id, day, "")).IsValid);
        Assert.False(new AttachWorkOrderPhotoValidator().Validate(new AttachWorkOrderPhoto(Guid.Empty, "", "")).IsValid);
        Assert.True(new AttachWorkOrderPhotoValidator().Validate(new AttachWorkOrderPhoto(id, "image/jpeg", "AA==")).IsValid);
        Assert.False(new RemoveFuneralValidator().Validate(new RemoveFuneral(Guid.Empty)).IsValid);
        Assert.True(new RemoveFuneralValidator().Validate(new RemoveFuneral(id)).IsValid);
        Assert.False(new RemoveChapelBookingValidator().Validate(new RemoveChapelBooking(Guid.Empty)).IsValid);
        Assert.True(new RemoveChapelBookingValidator().Validate(new RemoveChapelBooking(id)).IsValid);
        Assert.False(new RemoveWorkOrderValidator().Validate(new RemoveWorkOrder(Guid.Empty)).IsValid);
        Assert.True(new RemoveWorkOrderValidator().Validate(new RemoveWorkOrder(id)).IsValid);
        Assert.False(new RemoveMonumentPermitValidator().Validate(new RemoveMonumentPermit(Guid.Empty)).IsValid);
        Assert.True(new RemoveMonumentPermitValidator().Validate(new RemoveMonumentPermit(id)).IsValid);
    }

    [Fact]
    public async Task A_clerk_removes_a_funeral_booking_order_and_permit()
    {
        var world = new World();
        var funeral = Scheduled(world);
        world.Store.FindFuneralAsync(funeral.Id, Arg.Any<CancellationToken>()).Returns(funeral);
        var removedFuneral = await new RemoveFuneralHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
            .Handle(new RemoveFuneral(funeral.Id), CancellationToken.None);
        var missingFuneral = await Assert.ThrowsAsync<NotFoundException>(() =>
            new RemoveFuneralHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
                .Handle(new RemoveFuneral(Guid.CreateVersion7()), CancellationToken.None));

        var booking = ChapelBooking.Book(world.TenantId, world.Cemetery.Id, "chapel", "2026-06-01T10:00", "2026-06-01T11:00");
        world.Store.FindBookingAsync(booking.Id, Arg.Any<CancellationToken>()).Returns(booking);
        var removedBooking = await new RemoveChapelBookingHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
            .Handle(new RemoveChapelBooking(booking.Id), CancellationToken.None);
        var missingBooking = await Assert.ThrowsAsync<NotFoundException>(() =>
            new RemoveChapelBookingHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
                .Handle(new RemoveChapelBooking(Guid.CreateVersion7()), CancellationToken.None));

        var order = WorkOrder.Open(world.TenantId, world.Cemetery.Id, world.Site.Id, "digging", new DateOnly(2026, 6, 1), "");
        world.Store.FindWorkOrderAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        var removedOrder = await new RemoveWorkOrderHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
            .Handle(new RemoveWorkOrder(order.Id), CancellationToken.None);
        var missingOrder = await Assert.ThrowsAsync<NotFoundException>(() =>
            new RemoveWorkOrderHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
                .Handle(new RemoveWorkOrder(Guid.CreateVersion7()), CancellationToken.None));

        var permit = MonumentPermit.Issue(world.TenantId, world.Cemetery.Id, world.Site.Id, "Ana", new DateOnly(2026, 6, 1), new DateOnly(2026, 12, 1));
        world.Store.FindPermitAsync(permit.Id, Arg.Any<CancellationToken>()).Returns(permit);
        var removedPermit = await new RemoveMonumentPermitHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
            .Handle(new RemoveMonumentPermit(permit.Id), CancellationToken.None);
        var missingPermit = await Assert.ThrowsAsync<NotFoundException>(() =>
            new RemoveMonumentPermitHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
                .Handle(new RemoveMonumentPermit(Guid.CreateVersion7()), CancellationToken.None));

        Assert.True(removedFuneral);
        Assert.True(removedBooking);
        Assert.True(removedOrder);
        Assert.True(removedPermit);
        Assert.Equal("funeral.not_found", missingFuneral.Code);
        Assert.Equal("chapel.not_found", missingBooking.Code);
        Assert.Equal("work_order.not_found", missingOrder.Code);
        Assert.Equal("permit.not_found", missingPermit.Code);
        world.Store.Received(1).RemoveFuneral(funeral);
        world.Store.Received(1).RemoveBooking(booking);
        await world.Store.Received(1).RemoveWorkOrderAsync(order, Arg.Any<CancellationToken>());
        await world.Store.Received(1).RemovePermitAsync(permit, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_field_worker_cannot_remove_a_funeral()
    {
        var world = new World();
        world.Memberships.RoleInCurrentTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(MembershipRole.FieldWorker);
        var forbidden = await Assert.ThrowsAsync<ForbiddenException>(() =>
            new RemoveFuneralHandler(world.User, world.Tenant, world.Memberships, world.Store, world.Unit)
                .Handle(new RemoveFuneral(Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("register.forbidden", forbidden.Code);
    }

    private static ScheduleFuneralHandler Schedule(World world) =>
        new(world.User, world.Tenant, world.Memberships, world.Layouts, world.Register, world.Store, world.Unit);

    private static BookChapelHandler Book(World world) =>
        new(world.User, world.Tenant, world.Memberships, world.Layouts, world.Store, world.Unit);

    private static OpenWorkOrderHandler Open(World world) =>
        new(world.User, world.Tenant, world.Memberships, world.Layouts, world.Store, world.Unit);

    private static IssuePermitHandler Issue(World world) =>
        new(world.User, world.Tenant, world.Memberships, world.Layouts, world.Store, world.Unit, Clock());

    private static ScheduleFuneral Command(World world, string start, string end) =>
        new(world.Cemetery.Id, world.Person.Id, world.Site.Id, start, end, "chapel");

    private static Funeral Scheduled(World world) =>
        Funeral.Schedule(world.TenantId, world.Cemetery.Id, world.Person.Id, world.Site.Id, "2026-06-01T10:00", "2026-06-01T11:00", "chapel");

    private static Funeral Later(World world) =>
        Funeral.Schedule(world.TenantId, world.Cemetery.Id, world.Person.Id, world.Site.Id, "2026-06-01T15:00", "2026-06-01T16:00", "mortuary");

    private static FakeTimeProvider Clock() => new(Now);

    private static GeoPolygon Box() =>
        GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.001, 45),
            new GeoPoint(16.001, 45.001),
            new GeoPoint(16, 45.001),
            new GeoPoint(16, 45),
        ]);

    private sealed class World
    {
        public World()
        {
            Cemetery = CemeteryPlace.Open(TenantId, "Pilot", true, Now);
            Person = Deceased.Record(TenantId, Cemetery.Id, "Ana", "Horvat", null, null, Now);
            Site = GraveSite.Place(TenantId, Cemetery.Id, Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Box());
            User = Substitute.For<ICurrentUser>();
            User.UserId.Returns(Guid.CreateVersion7());
            Tenant = Substitute.For<ITenantContext>();
            Tenant.OrganizationId.Returns(TenantId);
            Memberships = Substitute.For<IMembershipRepository>();
            Memberships.RoleInCurrentTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(MembershipRole.CemeteryClerk);
            Layouts = Substitute.For<ILayoutRepository>();
            Layouts.FindCemeteryAsync(Cemetery.Id, Arg.Any<CancellationToken>()).Returns(Cemetery);
            Layouts.FindGraveSiteAsync(Site.Id, Arg.Any<CancellationToken>()).Returns(Site);
            Layouts.ListByCemeteryAsync(Cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { Site });
            Layouts.ListCemeteriesAsync(Arg.Any<CancellationToken>()).Returns(new[] { Cemetery });
            Register = Substitute.For<IRegisterRepository>();
            Register.FindDeceasedAsync(Person.Id, Arg.Any<CancellationToken>()).Returns(Person);
            Register.ListDeceasedAsync(Cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { Person });
            Store = Substitute.For<IOperationsRepository>();
            Store.ListFuneralsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Funeral>());
            Store.ListBookingsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<ChapelBooking>());
            Store.ListPermitsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<MonumentPermit>());
            Store.ListWorkOrdersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<WorkOrder>());
            Store.ListOpenOnAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<WorkOrder>());
            Unit = Substitute.For<IUnitOfWork>();
        }

        public Guid TenantId { get; } = Guid.CreateVersion7();

        public CemeteryPlace Cemetery { get; }

        public Deceased Person { get; }

        public GraveSite Site { get; }

        public ICurrentUser User { get; }

        public ITenantContext Tenant { get; }

        public IMembershipRepository Memberships { get; }

        public ILayoutRepository Layouts { get; }

        public IRegisterRepository Register { get; }

        public IOperationsRepository Store { get; }

        public IUnitOfWork Unit { get; }
    }
}

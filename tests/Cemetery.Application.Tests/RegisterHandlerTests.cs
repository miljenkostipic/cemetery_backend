using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Layout;
using Cemetery.Application.Register;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;
using NSubstitute;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Application.Tests;

public sealed class RegisterHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_clerk_records_a_deceased_person()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = CemeteryPlace.Open(tenant, "Pilot", true, Now);
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(cemetery);
        var register = Substitute.For<IRegisterRepository>();
        var handler = new RecordDeceasedHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, register, Substitute.For<IUnitOfWork>(), Clock());

        var created = await handler.Handle(new RecordDeceased(cemetery.Id, "Ana", "Horvat", null, new DateOnly(2020, 3, 4)), CancellationToken.None);

        Assert.Equal("Ana", created.GivenName);
        Assert.Equal("Horvat", created.FamilyName);
        Assert.Null(created.IntermentId);
        await register.Received(1).AddDeceasedAsync(Arg.Any<Deceased>(), Arg.Any<CancellationToken>());
        await register.Received(1).AddAuditAsync(Arg.Is<AuditEntry>(entry => entry.Action == AuditActions.DeceasedRecorded), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_field_worker_cannot_keep_the_register()
    {
        var handler = new RecordDeceasedHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.FieldWorker), Substitute.For<ILayoutRepository>(), Substitute.For<IRegisterRepository>(), Substitute.For<IUnitOfWork>(), Clock());

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new RecordDeceased(Guid.CreateVersion7(), "Ana", "Horvat", null, null), CancellationToken.None));

        Assert.Equal("register.forbidden", error.Code);
    }

    [Fact]
    public async Task A_clerk_buries_a_person_in_a_free_position()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = CemeteryPlace.Open(tenant, "Pilot", true, Now);
        var site = GraveSite.Place(tenant, cemetery.Id, Guid.CreateVersion7(), null, "A-0001", GraveSiteKind.SingleGrave, Rectangle());
        var person = Deceased.Record(tenant, cemetery.Id, "Ana", "Horvat", null, new DateOnly(2020, 3, 4), Now);
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindGraveSiteAsync(site.Id, Arg.Any<CancellationToken>()).Returns(site);
        layouts.FindCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(cemetery);
        var register = Substitute.For<IRegisterRepository>();
        register.FindDeceasedAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);
        register.ListIntermentsBySiteAsync(site.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<Interment>());
        register.ListIntermentsByDeceasedAsync(person.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<Interment>());
        var handler = new RecordIntermentHandler(User(), Tenant(tenant), Membership(MembershipRole.OrganizationAdmin), layouts, register, Substitute.For<IUnitOfWork>(), Clock());

        var buried = await handler.Handle(new RecordInterment(person.Id, site.Id, 1, "coffin", new DateOnly(2020, 4, 1)), CancellationToken.None);

        Assert.Equal(site.Code, buried.GraveSiteCode);
        Assert.Equal(1, buried.Position);
        Assert.Equal("coffin", buried.Kind);
        await register.Received(1).AddIntermentAsync(Arg.Any<Interment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_picker_hides_an_occupied_position()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = CemeteryPlace.Open(tenant, "Pilot", true, Now);
        var site = GraveSite.Place(tenant, cemetery.Id, Guid.CreateVersion7(), null, "A-0001", GraveSiteKind.Family, Rectangle(), 2);
        var buried = Interment.Record(tenant, cemetery.Id, Guid.CreateVersion7(), site.Id, 1, IntermentKind.Coffin, new DateOnly(2024, 1, 1));
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(cemetery);
        layouts.ListByCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { site });
        var register = Substitute.For<IRegisterRepository>();
        register.ListIntermentsByCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { buried });
        var handler = new ListFreePositionsHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, register, Clock());

        var open = await handler.Handle(new ListFreePositions(cemetery.Id, site.Id), CancellationToken.None);

        Assert.Single(open);
        Assert.Equal(2, open[0].Position);
        Assert.Equal("free", open[0].State);
        Assert.Contains("coffin", open[0].Kinds);
    }

    [Fact]
    public async Task A_grave_site_list_shows_occupancy()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = CemeteryPlace.Open(tenant, "Pilot", true, Now);
        var site = GraveSite.Place(tenant, cemetery.Id, Guid.CreateVersion7(), null, "A-0001", GraveSiteKind.SingleGrave, Rectangle());
        var buried = Interment.Record(tenant, cemetery.Id, Guid.CreateVersion7(), site.Id, 1, IntermentKind.Coffin, new DateOnly(2024, 1, 1));
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(cemetery);
        layouts.ListByCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { site });
        var register = Substitute.For<IRegisterRepository>();
        register.ListIntermentsByCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { buried });
        var handler = new ListGraveSitesHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, register, Clock());

        var sites = await handler.Handle(new ListGraveSites(cemetery.Id), CancellationToken.None);

        Assert.Equal("full", sites[0].Status);
    }

    [Fact]
    public async Task Removing_a_buried_person_frees_the_position()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = CemeteryPlace.Open(tenant, "Pilot", true, Now);
        var site = GraveSite.Place(tenant, cemetery.Id, Guid.CreateVersion7(), null, "A-0001", GraveSiteKind.SingleGrave, Rectangle());
        var person = Deceased.Record(tenant, cemetery.Id, "Ana", "Horvat", null, new DateOnly(2020, 3, 4), Now);
        var buried = Interment.Record(tenant, cemetery.Id, person.Id, site.Id, 1, IntermentKind.Coffin, new DateOnly(2020, 4, 1));
        var register = Substitute.For<IRegisterRepository>();
        register.FindDeceasedAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);
        register.ListIntermentsByDeceasedAsync(person.Id, Arg.Any<CancellationToken>()).Returns(new[] { buried });
        var handler = new RemoveDeceasedHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), register, Substitute.For<IUnitOfWork>(), Clock());

        Assert.True(await handler.Handle(new RemoveDeceased(person.Id), CancellationToken.None));

        Assert.NotNull(person.RemovedAt);
        Assert.False(buried.Occupies);
        await register.Received(1).AddAuditAsync(Arg.Is<AuditEntry>(entry => entry.Action == AuditActions.DeceasedRemoved), Arg.Any<CancellationToken>());
        await register.Received(1).AddAuditAsync(Arg.Is<AuditEntry>(entry => entry.Action == AuditActions.IntermentExhumed), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_correction_requires_the_person_to_exist()
    {
        var register = Substitute.For<IRegisterRepository>();
        register.FindDeceasedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Deceased?)null);
        var handler = new CorrectDeceasedHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.CemeteryClerk), register, Substitute.For<IUnitOfWork>(), Clock());

        var error = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new CorrectDeceased(Guid.CreateVersion7(), "Ana", "Kovač", null, null, "Spelling"), CancellationToken.None));

        Assert.Equal("deceased.not_found", error.Code);
    }

    private static ICurrentUser User()
    {
        var current = Substitute.For<ICurrentUser>();
        current.UserId.Returns(Guid.CreateVersion7());
        return current;
    }

    private static ITenantContext Tenant(Guid organizationId)
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.OrganizationId.Returns(organizationId);
        return tenant;
    }

    private static IMembershipRepository Membership(MembershipRole role)
    {
        var memberships = Substitute.For<IMembershipRepository>();
        memberships.RoleInCurrentTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(role);
        return memberships;
    }

    private static TimeProvider Clock()
    {
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Now);
        return clock;
    }

    private static GeoPolygon Rectangle() =>
        GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.001, 45),
            new GeoPoint(16.001, 45.001),
            new GeoPoint(16, 45.001),
            new GeoPoint(16, 45),
        ]);
}

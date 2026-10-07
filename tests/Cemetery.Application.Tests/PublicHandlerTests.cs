using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Catalog;
using Cemetery.Application.Register;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;
using NSubstitute;

namespace Cemetery.Application.Tests;

public sealed class PublicHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Search_refuses_a_name_that_is_too_short()
    {
        var handler = new SearchPublicHandler(Substitute.For<IPublicCatalog>(), Clock());

        var error = await Assert.ThrowsAsync<DomainRuleException>(() =>
            handler.Handle(new SearchPublic("A", null, null, null), CancellationToken.None));

        Assert.Equal("search.name_invalid", error.Code);
    }

    [Fact]
    public async Task Search_asks_the_catalog_with_the_trimmed_name()
    {
        var catalog = Substitute.For<IPublicCatalog>();
        catalog.SearchAsync("Ana", 1990, null, "pilot", new DateOnly(2026, 10, 7), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<PublicSearchHit>());
        var handler = new SearchPublicHandler(catalog, Clock());

        var hits = await handler.Handle(new SearchPublic(" Ana ", 1990, null, " pilot "), CancellationToken.None);

        Assert.Empty(hits);
    }

    [Fact]
    public async Task A_missing_memorial_is_not_found()
    {
        var catalog = Substitute.For<IPublicCatalog>();
        catalog.FindMemorialAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((PublicMemorial?)null);
        var handler = new GetMemorialHandler(catalog, Clock());

        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetMemorial(Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("memorial.not_found", error.Code);
    }

    [Fact]
    public async Task An_unknown_public_cemetery_is_not_found()
    {
        var catalog = Substitute.For<IPublicCatalog>();
        catalog.FindMapAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((PublicMap?)null);
        var handler = new GetPublicMapHandler(catalog);

        var error = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new GetPublicMap("pilot", Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("cemetery.not_found", error.Code);
    }

    [Fact]
    public async Task A_clerk_publishes_the_register_and_an_audit_entry_is_written()
    {
        var tenant = Guid.CreateVersion7();
        var organization = Organization.Create("Pilot", "pilot", Now);
        var organizations = Substitute.For<IOrganizationRepository>();
        organizations.FindAsync(tenant, Arg.Any<CancellationToken>()).Returns(organization);
        var register = Substitute.For<IRegisterRepository>();
        var handler = new PublishRegisterHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), organizations, register, Substitute.For<IUnitOfWork>(), Clock());

        var view = await handler.Handle(new PublishRegister(30), CancellationToken.None);

        Assert.True(view.PublishesRegister);
        Assert.Equal(30, view.HideRecentDeathsDays);
        await register.Received(1).AddAuditAsync(Arg.Is<AuditEntry>(entry => entry.Action == AuditActions.RegisterPublished), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_field_worker_cannot_publish_the_register()
    {
        var handler = new PublishRegisterHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.FieldWorker), Substitute.For<IOrganizationRepository>(), Substitute.For<IRegisterRepository>(), Substitute.For<IUnitOfWork>(), Clock());

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new PublishRegister(0), CancellationToken.None));

        Assert.Equal("register.forbidden", error.Code);
    }

    [Fact]
    public async Task A_clerk_hides_a_grave_from_the_public_map()
    {
        var tenant = Guid.CreateVersion7();
        var site = GraveSite.Place(tenant, Guid.CreateVersion7(), Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Ring());
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindGraveSiteAsync(site.Id, Arg.Any<CancellationToken>()).Returns(site);
        var register = Substitute.For<IRegisterRepository>();
        var handler = new HideGraveFromPublicHandler(User(), Tenant(tenant), Membership(MembershipRole.OrganizationAdmin), layouts, register, Substitute.For<IUnitOfWork>(), Clock());

        var view = await handler.Handle(new HideGraveFromPublic(site.Id), CancellationToken.None);

        Assert.True(view.HiddenFromPublic);
        await register.Received(1).AddAuditAsync(Arg.Is<AuditEntry>(entry => entry.Action == AuditActions.GraveHidden), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_clerk_describes_a_memorial()
    {
        var tenant = Guid.CreateVersion7();
        var person = Deceased.Record(tenant, Guid.CreateVersion7(), "Ana", "Horvat", null, new DateOnly(2020, 1, 1), Now);
        var register = Substitute.For<IRegisterRepository>();
        register.FindDeceasedAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);
        var handler = new DescribeMemorialHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), register, Substitute.For<IUnitOfWork>(), Clock());

        var view = await handler.Handle(new DescribeMemorial(person.Id, " Rest ", "https://example.com/a.jpg"), CancellationToken.None);

        Assert.Equal("Rest", view.Epitaph);
        Assert.Equal("https://example.com/a.jpg", view.PhotoUrl);
        await register.Received(1).AddAuditAsync(Arg.Is<AuditEntry>(entry => entry.Action == AuditActions.MemorialDescribed), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_clerk_withdraws_the_register()
    {
        var tenant = Guid.CreateVersion7();
        var organization = Organization.Create("Pilot", "pilot", Now);
        organization.PublishRegister(0);
        var organizations = Substitute.For<IOrganizationRepository>();
        organizations.FindAsync(tenant, Arg.Any<CancellationToken>()).Returns(organization);
        var register = Substitute.For<IRegisterRepository>();
        var handler = new WithdrawRegisterHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), organizations, register, Substitute.For<IUnitOfWork>(), Clock());

        var view = await handler.Handle(new WithdrawRegister(), CancellationToken.None);

        Assert.False(view.PublishesRegister);
        await register.Received(1).AddAuditAsync(Arg.Is<AuditEntry>(entry => entry.Action == AuditActions.RegisterWithdrawn), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_clerk_shows_a_grave_on_the_public_map_again()
    {
        var tenant = Guid.CreateVersion7();
        var site = GraveSite.Place(tenant, Guid.CreateVersion7(), Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Ring());
        site.HideFromPublic();
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindGraveSiteAsync(site.Id, Arg.Any<CancellationToken>()).Returns(site);
        var register = Substitute.For<IRegisterRepository>();
        var handler = new ShowGraveOnPublicMapHandler(User(), Tenant(tenant), Membership(MembershipRole.OrganizationAdmin), layouts, register, Substitute.For<IUnitOfWork>(), Clock());

        var view = await handler.Handle(new ShowGraveOnPublicMap(site.Id), CancellationToken.None);

        Assert.False(view.HiddenFromPublic);
        await register.Received(1).AddAuditAsync(Arg.Is<AuditEntry>(entry => entry.Action == AuditActions.GraveShown), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_clerk_reads_the_visibility_policy()
    {
        var tenant = Guid.CreateVersion7();
        var organization = Organization.Create("Pilot", "pilot", Now);
        organization.PublishRegister(7);
        var organizations = Substitute.For<IOrganizationRepository>();
        organizations.FindAsync(tenant, Arg.Any<CancellationToken>()).Returns(organization);
        var handler = new GetVisibilityHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), organizations);

        var view = await handler.Handle(new GetVisibility(), CancellationToken.None);

        Assert.True(view.PublishesRegister);
        Assert.Equal(7, view.HideRecentDeathsDays);
    }

    [Fact]
    public async Task A_blank_slug_is_not_a_public_cemetery()
    {
        var handler = new GetPublicMapHandler(Substitute.For<IPublicCatalog>());

        var error = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetPublicMap("  ", Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("cemetery.not_found", error.Code);
    }

    [Fact]
    public async Task The_catalog_lists_published_cemeteries()
    {
        var catalog = Substitute.For<IPublicCatalog>();
        var summary = new PublicCemeterySummary("pilot", "Pilot", Guid.CreateVersion7(), "North", true);
        catalog.ListCemeteriesAsync(Arg.Any<CancellationToken>()).Returns(new[] { summary });
        var handler = new ListPublicCemeteriesHandler(catalog);

        var listed = await handler.Handle(new ListPublicCemeteries(), CancellationToken.None);

        Assert.Equal(summary, Assert.Single(listed));
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

    private static GeoPolygon Ring() =>
        GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.001, 45),
            new GeoPoint(16.001, 45.001),
            new GeoPoint(16, 45.001),
            new GeoPoint(16, 45),
        ]);
}

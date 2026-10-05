using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Layout;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using NSubstitute;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Application.Tests;

public sealed class LayoutHandlerTests
{
    [Fact]
    public async Task A_clerk_opens_a_cemetery()
    {
        var layouts = Substitute.For<ILayoutRepository>();
        var handler = Handler(MembershipRole.CemeteryClerk, layouts);

        var created = await handler.Handle(new OpenCemetery("Pilot", true), CancellationToken.None);

        Assert.Equal("Pilot", created.Name);
        Assert.True(created.Schematic);
        await layouts.Received(1).AddCemeteryAsync(Arg.Any<CemeteryPlace>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_field_worker_cannot_map()
    {
        var handler = Handler(MembershipRole.FieldWorker, Substitute.For<ILayoutRepository>());

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new OpenCemetery("Pilot", false), CancellationToken.None));

        Assert.Equal("layout.forbidden", error.Code);
    }

    [Fact]
    public async Task Generating_a_grid_gives_every_site_an_id_type_and_geometry()
    {
        var tenant = Guid.CreateVersion7();
        var cemeteryId = Guid.CreateVersion7();
        var section = Section.Add(tenant, cemeteryId, "Field A", "A", null);
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindSectionAsync(section.Id, Arg.Any<CancellationToken>()).Returns(section);
        layouts.ListOpenInSectionAsync(section.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraveSite>());
        layouts.NextSequenceAsync(cemeteryId, Arg.Any<CancellationToken>()).Returns(1);
        layouts.CountRowsAsync(section.Id, Arg.Any<CancellationToken>()).Returns(0);
        var handler = new GenerateGraveSitesHandler(User(), Tenant(tenant), Membership(MembershipRole.OrganizationAdmin), layouts, Substitute.For<IUnitOfWork>());

        var sites = await handler.Handle(new GenerateGraveSites(section.Id, "single", 1, 2, 16, 45, 2, 1, 0.2, 0), CancellationToken.None);

        Assert.Equal(2, sites.Count);
        Assert.All(sites, site =>
        {
            Assert.NotEqual(Guid.Empty, site.Id);
            Assert.Equal("single", site.Kind);
            Assert.Equal(1, site.Capacity);
            Assert.Equal("available", site.Status);
            Assert.True(site.Outline.Count >= 4);
        });
    }

    private static OpenCemeteryHandler Handler(MembershipRole role, ILayoutRepository layouts) =>
        new(User(), Tenant(Guid.CreateVersion7()), Membership(role), layouts, Substitute.For<IUnitOfWork>(), TimeProvider.System);

    private static ICurrentUser User()
    {
        var current = Substitute.For<ICurrentUser>();
        current.UserId.Returns(Guid.CreateVersion7());
        return current;
    }

    [Fact]
    public async Task A_clerk_removes_a_section()
    {
        var tenant = Guid.CreateVersion7();
        var section = Section.Add(tenant, Guid.CreateVersion7(), "Field A", "A", null);
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindSectionAsync(section.Id, Arg.Any<CancellationToken>()).Returns(section);
        layouts.SectionHasClosedSiteAsync(section.Id, Arg.Any<CancellationToken>()).Returns(false);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new RemoveSectionHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, unitOfWork);

        var removed = await handler.Handle(new RemoveSection(section.Id), CancellationToken.None);

        Assert.True(removed);
        await layouts.Received(1).RemoveSectionAsync(section, Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_section_with_a_closed_grave_cannot_be_removed()
    {
        var tenant = Guid.CreateVersion7();
        var section = Section.Add(tenant, Guid.CreateVersion7(), "Field A", "A", null);
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindSectionAsync(section.Id, Arg.Any<CancellationToken>()).Returns(section);
        layouts.SectionHasClosedSiteAsync(section.Id, Arg.Any<CancellationToken>()).Returns(true);
        var handler = new RemoveSectionHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new RemoveSection(section.Id), CancellationToken.None));

        Assert.Equal("section.in_use", error.Code);
    }

    [Fact]
    public async Task A_closed_site_reopens_when_nothing_covers_it()
    {
        var tenant = Guid.CreateVersion7();
        var section = Section.Add(tenant, Guid.CreateVersion7(), "Field A", "A", null);
        var site = GraveSite.Place(tenant, section.CemeteryId, section.Id, null, "A-0001", GraveSiteKind.SingleGrave, Rectangle());
        site.Close();
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindGraveSiteAsync(site.Id, Arg.Any<CancellationToken>()).Returns(site);
        layouts.ListByCemeteryAsync(section.CemeteryId, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraveSite>());
        var handler = new ReopenGraveSiteHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

        var reopened = await handler.Handle(new ReopenGraveSite(site.Id), CancellationToken.None);

        Assert.Equal("available", reopened.Status);
    }

    [Fact]
    public async Task Reopening_a_split_site_removes_the_halves()
    {
        var tenant = Guid.CreateVersion7();
        var section = Section.Add(tenant, Guid.CreateVersion7(), "Field A", "A", null);
        var original = GraveSite.Place(tenant, section.CemeteryId, section.Id, null, "A-0001", GraveSiteKind.SingleGrave, Rectangle());
        var (left, right) = original.Split("A-0001-L", "A-0001-R0002");
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindGraveSiteAsync(original.Id, Arg.Any<CancellationToken>()).Returns(original);
        layouts.ListByCemeteryAsync(section.CemeteryId, Arg.Any<CancellationToken>()).Returns(new[] { left, right });
        var handler = new ReopenGraveSiteHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

        var restored = await handler.Handle(new ReopenGraveSite(original.Id), CancellationToken.None);

        Assert.Equal("available", restored.Status);
        await layouts.Received(1).RemoveGraveSitesAsync(
            Arg.Is<IReadOnlyList<GraveSite>>(sites => sites.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reopening_a_split_removes_a_closed_half_and_keeps_a_neighbour()
    {
        var tenant = Guid.CreateVersion7();
        var section = Section.Add(tenant, Guid.CreateVersion7(), "Field A", "A", null);
        var original = GraveSite.Place(tenant, section.CemeteryId, section.Id, null, "A-0001", GraveSiteKind.SingleGrave, Rectangle());
        var (left, right) = original.Split("A-0001-L", "A-0001-R0002");
        left.Close();
        var neighbour = GraveSite.Place(tenant, section.CemeteryId, section.Id, null, "A-0002", GraveSiteKind.SingleGrave, StickingOut());
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindGraveSiteAsync(original.Id, Arg.Any<CancellationToken>()).Returns(original);
        layouts.ListByCemeteryAsync(section.CemeteryId, Arg.Any<CancellationToken>()).Returns(new[] { left, right, neighbour });
        var handler = new ReopenGraveSiteHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

        var restored = await handler.Handle(new ReopenGraveSite(original.Id), CancellationToken.None);

        Assert.Equal("available", restored.Status);
        await layouts.Received(1).RemoveGraveSitesAsync(
            Arg.Is<IReadOnlyList<GraveSite>>(sites => sites.Count == 2 && sites.All(site => site.Id != neighbour.Id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reopening_is_rejected_when_another_grave_sticks_out()
    {
        var tenant = Guid.CreateVersion7();
        var section = Section.Add(tenant, Guid.CreateVersion7(), "Field A", "A", null);
        var site = GraveSite.Place(tenant, section.CemeteryId, section.Id, null, "A-0001", GraveSiteKind.SingleGrave, Rectangle());
        site.Close();
        var neighbour = GraveSite.Place(tenant, section.CemeteryId, section.Id, null, "A-0002", GraveSiteKind.SingleGrave, StickingOut());
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindGraveSiteAsync(site.Id, Arg.Any<CancellationToken>()).Returns(site);
        layouts.ListByCemeteryAsync(section.CemeteryId, Arg.Any<CancellationToken>()).Returns(new[] { neighbour });
        var handler = new ReopenGraveSiteHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new ReopenGraveSite(site.Id), CancellationToken.None));

        Assert.Equal("grave_site.reset_invalid", error.Code);
    }

    [Fact]
    public async Task Undoing_a_split_reopens_the_original_and_removes_the_halves()
    {
        var tenant = Guid.CreateVersion7();
        var section = Section.Add(tenant, Guid.CreateVersion7(), "Field A", "A", null);
        var original = GraveSite.Place(tenant, section.CemeteryId, section.Id, null, "A-0001", GraveSiteKind.SingleGrave, Rectangle());
        var (left, right) = original.Split("A-0001-L", "A-0001-R0002");
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindGraveSiteAsync(original.Id, Arg.Any<CancellationToken>()).Returns(original);
        layouts.FindGraveSiteAsync(left.Id, Arg.Any<CancellationToken>()).Returns(left);
        layouts.FindGraveSiteAsync(right.Id, Arg.Any<CancellationToken>()).Returns(right);
        var handler = new UndoGraveSiteSplitHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

        var restored = await handler.Handle(new UndoGraveSiteSplit(original.Id, left.Id, right.Id), CancellationToken.None);

        Assert.Equal("available", restored.Status);
        Assert.Equal(original.Id, restored.Id);
        await layouts.Received(1).RemoveGraveSitesAsync(
            Arg.Is<IReadOnlyList<GraveSite>>(sites => sites.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Two_different_sites_pass_merge_validation()
    {
        var validator = new MergeGraveSitesValidator();

        var result = await validator.ValidateAsync(new MergeGraveSites(Guid.CreateVersion7(), Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Merging_a_site_with_itself_is_rejected()
    {
        var validator = new MergeGraveSitesValidator();
        var id = Guid.CreateVersion7();

        var result = await validator.ValidateAsync(new MergeGraveSites(id, id), TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, error => error.ErrorCode == "grave_site.merge_invalid");
    }

    private static GeoPolygon Rectangle() =>
        GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.002, 45),
            new GeoPoint(16.002, 45.001),
            new GeoPoint(16, 45.001),
            new GeoPoint(16, 45),
        ]);

    private static GeoPolygon StickingOut() =>
        GeoPolygon.Create([
            new GeoPoint(16.001, 45),
            new GeoPoint(16.004, 45),
            new GeoPoint(16.004, 45.001),
            new GeoPoint(16.001, 45.001),
            new GeoPoint(16.001, 45),
        ]);

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
}

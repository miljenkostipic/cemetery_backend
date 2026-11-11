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
    public async Task Generating_a_grid_gives_every_site_an_id_type_and_geometry()
    {
        var tenant = Guid.CreateVersion7();
        var cemeteryId = Guid.CreateVersion7();
        var section = Section.Add(tenant, cemeteryId, "Field A", "A", null);
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindSectionAsync(section.Id, Arg.Any<CancellationToken>()).Returns(section);
        layouts.ListOpenInSectionAsync(section.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraveSite>());
        layouts.CountRowsAsync(section.Id, Arg.Any<CancellationToken>()).Returns(2);
        layouts.ListGraveSiteCodesAsync(cemeteryId, Arg.Any<CancellationToken>()).Returns(["A-0001"]);
        var handler = new GenerateGraveSitesHandler(Callers.For(tenant, MembershipRole.OrganizationAdmin), layouts, Substitute.For<IUnitOfWork>());

        var sites = await handler.Handle(new GenerateGraveSites(section.Id, "single", 1, 2, 16, 45, 2, 1, 0.2, 0), CancellationToken.None);

        Assert.Equal(["A-03-01", "A-03-02"], sites.Select(site => site.Code));
        Assert.All(sites, site =>
        {
            Assert.NotEqual(Guid.Empty, site.Id);
            Assert.Equal("single", site.Kind);
            Assert.Equal(1, site.Capacity);
            Assert.Equal("available", site.Status);
            Assert.True(site.Outline.Count >= 4);
        });
    }

    [Fact]
    public async Task Generating_refuses_a_code_the_cemetery_already_uses()
    {
        var tenant = Guid.CreateVersion7();
        var section = Section.Add(tenant, Guid.CreateVersion7(), "Field A", "A", null);
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindSectionAsync(section.Id, Arg.Any<CancellationToken>()).Returns(section);
        layouts.ListOpenInSectionAsync(section.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraveSite>());
        layouts.ListGraveSiteCodesAsync(section.CemeteryId, Arg.Any<CancellationToken>()).Returns(["A-01-02"]);
        var handler = new GenerateGraveSitesHandler(Callers.For(tenant, MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new GenerateGraveSites(section.Id, "single", 1, 2, 16, 45, 2, 1, 0.2, 0), CancellationToken.None));

        Assert.Equal("grave_site.code_taken", error.Code);
        await layouts.DidNotReceive().AddGraveSiteAsync(Arg.Any<GraveSite>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_drawn_grave_without_a_row_takes_the_next_row_less_number()
    {
        var (section, layouts) = DrawWorld();
        layouts.ListRowCodesAsync(section.Id, null, Arg.Any<CancellationToken>()).Returns(["A-007"]);
        var handler = DrawHandler(section, layouts);

        var site = await handler.Handle(new DrawGraveSite(section.Id, Ring(Grave()), "family", null, null, null), CancellationToken.None);

        Assert.Equal("A-008", site.Code);
        Assert.Null(site.RowId);
        Assert.Equal(2, site.Capacity);
    }

    [Fact]
    public async Task A_drawn_grave_in_a_row_takes_the_next_place()
    {
        var (section, layouts) = DrawWorld();
        var row = GraveRow.Add(section.TenantId, section.Id, "4");
        layouts.ListRowsAsync(section.Id, Arg.Any<CancellationToken>()).Returns([row]);
        layouts.ListRowCodesAsync(section.Id, row.Id, Arg.Any<CancellationToken>()).Returns(["A-04-01"]);
        var handler = DrawHandler(section, layouts);

        var site = await handler.Handle(new DrawGraveSite(section.Id, Ring(Grave()), "single", 1, null, row.Id), CancellationToken.None);

        Assert.Equal("A-04-02", site.Code);
        Assert.Equal(row.Id, site.RowId);
    }

    [Fact]
    public async Task A_drawn_grave_keeps_a_free_code_and_refuses_a_taken_one()
    {
        var (section, layouts) = DrawWorld();
        layouts.ListGraveSiteCodesAsync(section.CemeteryId, Arg.Any<CancellationToken>()).Returns(["A-001"]);
        var handler = DrawHandler(section, layouts);

        var site = await handler.Handle(new DrawGraveSite(section.Id, Ring(Grave()), "single", null, " A-OLD-7 ", null), CancellationToken.None);
        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new DrawGraveSite(section.Id, Ring(Grave()), "single", null, "A-001", null), CancellationToken.None));

        Assert.Equal("A-OLD-7", site.Code);
        Assert.Equal("grave_site.code_taken", error.Code);
    }

    [Fact]
    public async Task A_drawn_grave_in_a_row_of_another_section_is_not_found()
    {
        var (section, layouts) = DrawWorld();
        layouts.ListRowsAsync(section.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraveRow>());
        var handler = DrawHandler(section, layouts);

        var error = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DrawGraveSite(section.Id, Ring(Grave()), "single", null, null, Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("grave_row.not_found", error.Code);
    }

    [Fact]
    public async Task Drawing_validates_outline_kind_code_and_capacity()
    {
        var validator = new DrawGraveSiteValidator();
        var tooMany = Enumerable.Range(0, GraveSite.OutlineMaxPoints + 1).Select(_ => new GeoPointView(16, 45)).ToArray();

        var result = await validator.ValidateAsync(new DrawGraveSite(Guid.CreateVersion7(), tooMany, "nope", -1, new string('X', GraveSite.CodeMaxLength + 1), null), TestContext.Current.CancellationToken);
        var blank = await validator.ValidateAsync(new DrawGraveSite(Guid.CreateVersion7(), Ring(Grave()), "single", null, " ", null), TestContext.Current.CancellationToken);
        var fine = await validator.ValidateAsync(new DrawGraveSite(Guid.CreateVersion7(), Ring(Grave()), "single", 1, "A-1", null), TestContext.Current.CancellationToken);

        Assert.Equal(
            ["geometry.ring_invalid", "grave_site.outline_too_complex", "grave_site.kind_invalid", "grave_site.code_invalid", "grave_site.capacity_invalid"],
            result.Errors.Select(error => error.ErrorCode));
        Assert.Equal(["grave_site.code_invalid"], blank.Errors.Select(error => error.ErrorCode));
        Assert.True(fine.IsValid);
    }

    private static (Section Section, ILayoutRepository Layouts) DrawWorld()
    {
        var section = Section.Add(Guid.CreateVersion7(), Guid.CreateVersion7(), "Field A", "A", null);
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindSectionAsync(section.Id, Arg.Any<CancellationToken>()).Returns(section);
        layouts.ListOpenInSectionAsync(section.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraveSite>());
        layouts.ListGraveSiteCodesAsync(section.CemeteryId, Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
        layouts.ListRowCodesAsync(section.Id, Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<string>());
        return (section, layouts);
    }

    private static DrawGraveSiteHandler DrawHandler(Section section, ILayoutRepository layouts) =>
        new(Callers.For(section.TenantId, MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

    private static GeoPointView[] Ring(GeoPolygon polygon) =>
        polygon.Ring.Select(point => new GeoPointView(point.Longitude, point.Latitude)).ToArray();

    private static OpenCemeteryHandler Handler(MembershipRole role, ILayoutRepository layouts) =>
        new(Callers.For(Guid.CreateVersion7(), role), layouts, Substitute.For<IUnitOfWork>(), TimeProvider.System);


    [Fact]
    public async Task A_clerk_removes_a_section()
    {
        var tenant = Guid.CreateVersion7();
        var section = Section.Add(tenant, Guid.CreateVersion7(), "Field A", "A", null);
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindSectionAsync(section.Id, Arg.Any<CancellationToken>()).Returns(section);
        layouts.SectionHasClosedSiteAsync(section.Id, Arg.Any<CancellationToken>()).Returns(false);
        var unitOfWork = Substitute.For<IUnitOfWork>();
        var handler = new RemoveSectionHandler(Callers.For(tenant, MembershipRole.CemeteryClerk), layouts, unitOfWork);

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
        var handler = new RemoveSectionHandler(Callers.For(tenant, MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

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
        var handler = new ReopenGraveSiteHandler(Callers.For(tenant, MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

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
        var handler = new ReopenGraveSiteHandler(Callers.For(tenant, MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

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
        var handler = new ReopenGraveSiteHandler(Callers.For(tenant, MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

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
        var handler = new ReopenGraveSiteHandler(Callers.For(tenant, MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

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
        var handler = new UndoGraveSiteSplitHandler(Callers.For(tenant, MembershipRole.CemeteryClerk), layouts, Substitute.For<IUnitOfWork>());

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

    /// <summary>About 1.6 m by 2.2 m.</summary>
    private static GeoPolygon Grave() =>
        GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.00002, 45),
            new GeoPoint(16.00002, 45.00002),
            new GeoPoint(16, 45.00002),
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


}

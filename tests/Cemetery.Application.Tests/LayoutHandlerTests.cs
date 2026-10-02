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

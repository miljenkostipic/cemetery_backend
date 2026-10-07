using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Rights;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Rights;
using NSubstitute;

namespace Cemetery.Application.Tests;

public sealed class OfficeRequestHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_clerk_lists_office_requests()
    {
        var tenant = Guid.CreateVersion7();
        var author = Guid.CreateVersion7();
        var stored = OfficeRequest.Send(tenant, author, "Please call", null, new DateOnly(2026, 10, 7));
        var requests = Substitute.For<IOfficeRequestRepository>();
        requests.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { stored });
        var handler = new ListOfficeRequestsHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), requests);

        var listed = await handler.Handle(new ListOfficeRequests(), CancellationToken.None);

        Assert.Equal([stored.Id], listed.Select(item => item.Id));
        Assert.Equal("Please call", listed[0].Message);
    }

    [Fact]
    public async Task A_grave_holder_cannot_read_the_office_list()
    {
        var handler = new ListOfficeRequestsHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.GraveHolder), Substitute.For<IOfficeRequestRepository>());

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new ListOfficeRequests(), CancellationToken.None));

        Assert.Equal("register.forbidden", error.Code);
    }

    [Fact]
    public async Task A_member_sends_a_request()
    {
        var requests = Substitute.For<IOfficeRequestRepository>();
        var handler = new SendOfficeRequestHandler(
            User(),
            Tenant(Guid.CreateVersion7()),
            Membership(MembershipRole.GraveHolder),
            Substitute.For<ILayoutRepository>(),
            requests,
            Substitute.For<IUnitOfWork>(),
            Clock());

        var created = await handler.Handle(new SendOfficeRequest(" Please call ", null), CancellationToken.None);

        Assert.Equal("Please call", created.Message);
        Assert.Equal(new DateOnly(2026, 10, 7), created.CreatedOn);
        await requests.Received(1).AddAsync(Arg.Any<OfficeRequest>(), Arg.Any<CancellationToken>());
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
}

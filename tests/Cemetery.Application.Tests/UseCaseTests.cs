using Cemetery.Application.Abstractions;
using Cemetery.Application.Auth;
using Cemetery.Application.Dashboard;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Organizations;
using Cemetery.Domain.Identity;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Cemetery.Application.Tests;

public sealed class CreateOrganizationHandlerTests
{
    [Fact]
    public async Task Creates_an_organization_and_selects_it()
    {
        var userId = Guid.CreateVersion7();
        var current = Substitute.For<ICurrentUser>();
        current.UserId.Returns(userId);
        var organizations = Substitute.For<IOrganizationRepository>();
        organizations.SlugExistsAsync("alpha-cemetery", Arg.Any<CancellationToken>()).Returns(false);
        var memberships = Substitute.For<IMembershipRepository>();
        var tenant = new FakeTenant();
        var active = Substitute.For<IActiveOrganizationStore>();
        var handler = new CreateOrganizationHandler(
            current,
            tenant,
            organizations,
            memberships,
            Substitute.For<IUnitOfWork>(),
            active,
            new FakeTimeProvider(new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)));

        var created = await handler.Handle(new CreateOrganization("Alpha Cemetery"), CancellationToken.None);

        Assert.Equal("alpha-cemetery", created.Slug);
        Assert.Equal(MembershipRoles.OrganizationAdmin, created.Role);
        Assert.Equal(created.Id, tenant.OrganizationId);
        active.Received(1).Remember(created.Id);
        await memberships.Received(1).AddAsync(
            Arg.Is<Membership>(membership => membership.UserId == userId && membership.TenantId == created.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejects_a_taken_slug()
    {
        var current = Substitute.For<ICurrentUser>();
        current.UserId.Returns(Guid.CreateVersion7());
        var organizations = Substitute.For<IOrganizationRepository>();
        organizations.SlugExistsAsync("alpha", Arg.Any<CancellationToken>()).Returns(true);
        var handler = new CreateOrganizationHandler(
            current,
            new FakeTenant(),
            organizations,
            Substitute.For<IMembershipRepository>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IActiveOrganizationStore>(),
            TimeProvider.System);

        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new CreateOrganization("Alpha"), CancellationToken.None));

        Assert.Equal("organization.slug_taken", error.Code);
    }

    [Fact]
    public async Task Rejects_an_anonymous_caller()
    {
        var handler = new CreateOrganizationHandler(
            Substitute.For<ICurrentUser>(),
            new FakeTenant(),
            Substitute.For<IOrganizationRepository>(),
            Substitute.For<IMembershipRepository>(),
            Substitute.For<IUnitOfWork>(),
            Substitute.For<IActiveOrganizationStore>(),
            TimeProvider.System);

        var error = await Assert.ThrowsAsync<UnauthorizedException>(() => handler.Handle(new CreateOrganization("Alpha"), CancellationToken.None));

        Assert.Equal("auth.required", error.Code);
    }
}

public sealed class DashboardAndInviteTests
{
    [Fact]
    public async Task Dashboard_fails_closed_when_the_organization_is_missing()
    {
        var handler = new GetDashboardHandler(
            Callers.For(Guid.CreateVersion7(), MembershipRole.CemeteryClerk),
            Substitute.For<IOrganizationRepository>(),
            Substitute.For<ILayoutRepository>());

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new GetDashboard(), CancellationToken.None));

        Assert.Equal("tenant.forbidden", error.Code);
    }

    [Fact]
    public async Task Dashboard_is_empty_for_the_current_tenant()
    {
        var organization = Organization.Create("Alpha", "alpha", DateTimeOffset.UnixEpoch);
        var organizations = Substitute.For<IOrganizationRepository>();
        organizations.FindAsync(organization.Id, Arg.Any<CancellationToken>()).Returns(organization);
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.CountGraveSitesAsync(Arg.Any<CancellationToken>()).Returns(0);
        var handler = new GetDashboardHandler(Callers.For(organization.Id, MembershipRole.OrganizationAdmin), organizations, layouts);

        var dashboard = await handler.Handle(new GetDashboard(), CancellationToken.None);

        Assert.Equal(organization.Id, dashboard.OrganizationId);
        Assert.Equal(0, dashboard.GraveSiteCount);
    }

    [Fact]
    public async Task Invite_refuses_a_clerk()
    {
        var handler = Invite(Callers.For(Guid.CreateVersion7(), MembershipRole.CemeteryClerk));

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new InviteMember("holder@example.com", MembershipRoles.GraveHolder), CancellationToken.None));

        Assert.Equal("access.forbidden", error.Code);
    }

    [Fact]
    public async Task Switch_refuses_another_organization()
    {
        var current = Substitute.For<ICurrentUser>();
        current.UserId.Returns(Guid.CreateVersion7());
        var organizations = Substitute.For<IOrganizationRepository>();
        organizations.ListForUserAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<OrganizationMembership>());
        var handler = new SwitchOrganizationHandler(current, new FakeTenant(), organizations, Substitute.For<IActiveOrganizationStore>());

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            handler.Handle(new SwitchOrganization(Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("tenant.forbidden", error.Code);
    }

    private static InviteMemberHandler Invite(ICallerContext caller) =>
        new(
            caller,
            Substitute.For<IInvitationRepository>(),
            Substitute.For<IInvitationTokenFactory>(),
            Substitute.For<IEmailSender>(),
            Substitute.For<IAppLinks>(),
            Substitute.For<IUnitOfWork>(),
            TimeProvider.System);
}

public sealed class LoginUserHandlerTests
{
    [Fact]
    public async Task Does_not_reveal_which_check_failed()
    {
        var passwords = Substitute.For<IPasswordSignIn>();
        passwords.SignInAsync(Arg.Any<EmailAddress>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PasswordSignInStatus.Failed);
        var handler = new LoginUserHandler(passwords, Substitute.For<IUserAccountGateway>());

        var error = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            handler.Handle(new LoginUser("a@example.com", "Correct-Horse-1"), CancellationToken.None));

        Assert.Equal("auth.failed", error.Code);
    }
}

file sealed class FakeTenant : ITenantContext
{
    public Guid? OrganizationId { get; private set; }

    public void Use(Guid organizationId) => OrganizationId = organizationId;
}

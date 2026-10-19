using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Launch;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Launch;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;
using NSubstitute;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Application.Tests;

public sealed class LaunchHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_clerk_reads_occupancy_and_an_upcoming_rest_period()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = CemeteryPlace.Open(tenant, "Pilot", true, Now);
        var site = GraveSite.Place(tenant, cemetery.Id, Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Box());
        var buried = Interment.Record(tenant, cemetery.Id, Guid.CreateVersion7(), site.Id, 1, IntermentKind.Coffin, new DateOnly(2011, 10, 15));
        var layouts = Layouts(cemetery, site);
        var register = Substitute.For<IRegisterRepository>();
        register.ListIntermentsByCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { buried });
        var handler = new GetCemeteryReportHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, register, Clock());

        var report = await handler.Handle(new GetCemeteryReport(cemetery.Id), CancellationToken.None);

        Assert.Equal(1, report.Occupancy.SiteCount);
        Assert.Equal(0, report.Occupancy.FullSites);
        Assert.Equal(1, report.FreeCapacity.Capacity);
        Assert.Equal(1, report.FreeCapacity.ReusablePositions);
        Assert.Equal(new DateOnly(2026, 10, 15), report.RestPeriodEnds[0].EndsOn);
        Assert.Equal("A-1", report.RestPeriodEnds[0].GraveSiteCode);
        Assert.Empty(report.Revenue);
    }

    [Fact]
    public async Task A_missing_cemetery_has_no_report()
    {
        var handler = new GetCemeteryReportHandler(
            User(),
            Tenant(Guid.CreateVersion7()),
            Membership(MembershipRole.OrganizationAdmin),
            Substitute.For<ILayoutRepository>(),
            Substitute.For<IRegisterRepository>(),
            Clock());

        var error = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetCemeteryReport(Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("report.not_found", error.Code);
    }

    [Fact]
    public async Task A_clerk_registers_a_webhook_and_sees_the_secret_once()
    {
        var webhooks = Substitute.For<IWebhookRepository>();
        var handler = new AddWebhookHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.OrganizationAdmin), webhooks, Substitute.For<IUnitOfWork>());

        var created = await handler.Handle(new AddWebhook(" https://example.com/hook ", "payment.received"), CancellationToken.None);

        Assert.Equal("https://example.com/hook", created.Url);
        Assert.Equal(WebhookEvents.PaymentReceived, created.Event);
        Assert.False(string.IsNullOrWhiteSpace(created.Secret));
        await webhooks.Received(1).AddAsync(Arg.Any<WebhookSubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_webhook_cannot_be_removed()
    {
        var handler = new RemoveWebhookHandler(
            User(),
            Tenant(Guid.CreateVersion7()),
            Membership(MembershipRole.CemeteryClerk),
            Substitute.For<IWebhookRepository>(),
            Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new RemoveWebhook(Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("webhook.not_found", error.Code);
    }

    [Fact]
    public async Task A_clerk_links_a_death_certificate()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = CemeteryPlace.Open(tenant, "Pilot", true, Now);
        var site = GraveSite.Place(tenant, cemetery.Id, Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Box());
        var person = Deceased.Record(tenant, cemetery.Id, "Ana", "Horvat", null, null, Now);
        var interment = Interment.Record(tenant, cemetery.Id, person.Id, site.Id, 1, IntermentKind.Urn, new DateOnly(2020, 4, 1));
        var register = Substitute.For<IRegisterRepository>();
        register.FindIntermentAsync(interment.Id, Arg.Any<CancellationToken>()).Returns(interment);
        register.FindDeceasedAsync(person.Id, Arg.Any<CancellationToken>()).Returns(person);
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindGraveSiteAsync(site.Id, Arg.Any<CancellationToken>()).Returns(site);
        var links = Substitute.For<IMaticaLinkRepository>();
        var handler = new LinkDeathCertificateHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), register, layouts, links, Substitute.For<IUnitOfWork>());
        var certificate = Guid.CreateVersion7();

        var linked = await handler.Handle(new LinkDeathCertificate(interment.Id, certificate), CancellationToken.None);

        Assert.Equal("Ana Horvat", linked.DeceasedName);
        Assert.Equal("A-1", linked.GraveSiteCode);
        Assert.Equal(certificate, linked.DeathCertificateId);
        await links.Received(1).AddAsync(Arg.Any<MaticaLink>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_certificate_already_linked_is_refused()
    {
        var links = Substitute.For<IMaticaLinkRepository>();
        links.FindByCertificateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(MaticaLink.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()));
        var register = Substitute.For<IRegisterRepository>();
        register.FindIntermentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Interment.Record(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1, IntermentKind.Coffin, new DateOnly(2020, 1, 1)));
        var handler = new LinkDeathCertificateHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.OrganizationAdmin), register, Substitute.For<ILayoutRepository>(), links, Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new LinkDeathCertificate(Guid.CreateVersion7(), Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("matica.already_linked", error.Code);
    }

    [Fact]
    public async Task A_clerk_reads_the_processing_register_and_requests_an_export()
    {
        var exports = Substitute.For<IGdprExportRepository>();
        var records = new ListGdprRecordsHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.CemeteryClerk));
        var export = new RequestGdprExportHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.OrganizationAdmin), exports, Substitute.For<IUnitOfWork>(), Clock());

        var listed = await records.Handle(new ListGdprRecords(), CancellationToken.None);
        var requested = await export.Handle(new RequestGdprExport(), CancellationToken.None);

        Assert.Contains(listed, item => item.Purpose == "holders" && item.LawfulBasis == "contract");
        Assert.Equal(GdprExport.Requested, requested.Status);
        Assert.Equal(Now, requested.RequestedAt);
        await exports.Received(1).AddAsync(Arg.Any<GdprExport>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_grave_holder_cannot_read_reports()
    {
        var handler = new GetCemeteryReportHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.GraveHolder), Substitute.For<ILayoutRepository>(), Substitute.For<IRegisterRepository>(), Clock());

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new GetCemeteryReport(Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("register.forbidden", error.Code);
    }

    [Fact]
    public async Task An_unknown_cemetery_has_no_interment_list()
    {
        var handler = new ListCemeteryIntermentsHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.CemeteryClerk), Substitute.For<ILayoutRepository>(), Substitute.For<IRegisterRepository>());

        var error = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ListCemeteryInterments(Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("cemetery.not_found", error.Code);
    }

    [Fact]
    public void A_validator_rejects_an_insecure_webhook()
    {
        var result = new AddWebhookValidator().Validate(new AddWebhook("http://example.com/hook", "nope"));

        Assert.Contains(result.Errors, error => error.ErrorCode == "webhook.url_invalid");
        Assert.Contains(result.Errors, error => error.ErrorCode == "webhook.event_invalid");
    }

    [Fact]
    public async Task A_clerk_lists_webhooks_without_the_secret_and_can_remove_one()
    {
        var tenant = Guid.CreateVersion7();
        var stored = WebhookSubscription.Register(tenant, "https://example.com/hook", WebhookEvents.IntermentRecorded, "secret-value");
        var webhooks = Substitute.For<IWebhookRepository>();
        webhooks.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { stored });
        webhooks.FindAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);
        var listed = await new ListWebhooksHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), webhooks).Handle(new ListWebhooks(), CancellationToken.None);
        var removed = await new RemoveWebhookHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), webhooks, Substitute.For<IUnitOfWork>()).Handle(new RemoveWebhook(stored.Id), CancellationToken.None);

        Assert.Equal(stored.Url, listed[0].Url);
        Assert.True(removed);
        webhooks.Received(1).Remove(stored);
    }

    [Fact]
    public async Task A_missing_interment_cannot_be_linked()
    {
        var handler = new LinkDeathCertificateHandler(
            User(),
            Tenant(Guid.CreateVersion7()),
            Membership(MembershipRole.CemeteryClerk),
            Substitute.For<IRegisterRepository>(),
            Substitute.For<ILayoutRepository>(),
            Substitute.For<IMaticaLinkRepository>(),
            Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => handler.Handle(new LinkDeathCertificate(Guid.CreateVersion7(), Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("matica.interment_invalid", error.Code);
    }

    [Fact]
    public async Task The_register_list_skips_an_interment_whose_person_was_removed()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = CemeteryPlace.Open(tenant, "Pilot", true, Now);
        var site = GraveSite.Place(tenant, cemetery.Id, Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Box());
        site.Close();
        var buried = Interment.Record(tenant, cemetery.Id, Guid.CreateVersion7(), site.Id, 1, IntermentKind.Coffin, new DateOnly(2000, 1, 1));
        buried.Exhume(new DateOnly(2001, 1, 1));
        var later = Interment.Record(tenant, cemetery.Id, Guid.CreateVersion7(), site.Id, 1, IntermentKind.Coffin, new DateOnly(2024, 1, 1));
        var layouts = Layouts(cemetery, site);
        var register = Substitute.For<IRegisterRepository>();
        register.ListIntermentsByCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { buried, later });
        register.ListDeceasedAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(Array.Empty<Deceased>());
        var links = Substitute.For<IMaticaLinkRepository>();
        var missing = MaticaLink.Create(tenant, cemetery.Id, Guid.CreateVersion7(), Guid.CreateVersion7());
        links.ListByCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { MaticaLink.Create(tenant, cemetery.Id, buried.Id, Guid.CreateVersion7()), missing });
        var interments = new ListCemeteryIntermentsHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, register);
        var listedLinks = new ListMaticaLinksHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, register, links);

        var rows = await interments.Handle(new ListCemeteryInterments(cemetery.Id), CancellationToken.None);
        var linked = await listedLinks.Handle(new ListMaticaLinks(cemetery.Id), CancellationToken.None);
        var report = await new GetCemeteryReportHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, register, Clock()).Handle(new GetCemeteryReport(cemetery.Id), CancellationToken.None);

        Assert.Empty(rows);
        Assert.Equal("", linked[0].DeceasedName);
        Assert.Equal("A-1", linked[0].GraveSiteCode);
        Assert.Equal("", linked[1].GraveSiteCode);
        Assert.Equal(1, report.Occupancy.ClosedSites);
        Assert.Empty(report.RestPeriodEnds);
        Assert.Equal(0, report.FreeCapacity.Capacity);
    }

    private static ILayoutRepository Layouts(CemeteryPlace cemetery, GraveSite site)
    {
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(cemetery);
        layouts.ListByCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { site });
        return layouts;
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

    private static GeoPolygon Box() =>
        GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.001, 45),
            new GeoPoint(16.001, 45.001),
            new GeoPoint(16, 45.001),
            new GeoPoint(16, 45),
        ]);
}

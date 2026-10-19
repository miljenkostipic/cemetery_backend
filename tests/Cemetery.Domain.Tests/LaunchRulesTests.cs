using Cemetery.Domain.Identity;
using Cemetery.Domain.Launch;
using Cemetery.Domain.Register;

namespace Cemetery.Domain.Tests;

public sealed class LaunchRulesTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    [Fact]
    public void An_https_address_is_kept()
    {
        var canonical = WebhookAddress.Require(" https://example.com/hook ");

        Assert.Equal("https://example.com/hook", canonical);
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://example.com/hook")]
    [InlineData("https://user:secret@example.com/hook")]
    [InlineData("https://localhost/hook")]
    [InlineData("https://api.localhost/hook")]
    [InlineData("https://printer.local/hook")]
    [InlineData("https://1.2.3.4/hook")]
    [InlineData("https://[::1]/hook")]
    [InlineData("https://999.1.1.1/hook")]
    [InlineData("https://./hook")]
    public void A_private_or_insecure_address_is_refused(string url)
    {
        var error = Assert.Throws<DomainRuleException>(() => WebhookAddress.Require(url));

        Assert.Equal("webhook.url_invalid", error.Code);
    }

    [Fact]
    public void A_numeric_label_that_is_not_an_address_is_allowed()
    {
        Assert.Equal("https://1.2.3.a/hook", WebhookAddress.Require("https://1.2.3.a/hook"));
    }

    [Fact]
    public void A_control_character_is_refused()
    {
        var error = Assert.Throws<DomainRuleException>(() => WebhookAddress.Require("https://exam\nple.com/hook"));

        Assert.Equal("webhook.url_invalid", error.Code);
    }

    [Fact]
    public void A_subscription_keeps_the_secret_and_the_event()
    {
        var created = WebhookSubscription.Register(Guid.CreateVersion7(), "https://example.com/hook", " interment.recorded ", "secret-value");

        Assert.Equal("https://example.com/hook", created.Url);
        Assert.Equal(WebhookEvents.IntermentRecorded, created.Event);
        Assert.Equal("secret-value", created.Secret);
    }

    [Fact]
    public void An_unknown_event_is_refused()
    {
        var error = Assert.Throws<DomainRuleException>(() =>
            WebhookSubscription.Register(Guid.CreateVersion7(), "https://example.com/hook", "other", "secret"));

        Assert.Equal("webhook.event_invalid", error.Code);
    }

    [Fact]
    public void A_delivery_waits_to_be_sent()
    {
        var at = new DateTimeOffset(2026, 10, 15, 8, 0, 0, TimeSpan.Zero);
        var delivery = WebhookDelivery.Queue(Guid.CreateVersion7(), Guid.CreateVersion7(), WebhookEvents.FuneralScheduled, Guid.CreateVersion7(), at);

        Assert.Equal(WebhookDelivery.Pending, delivery.Status);
        Assert.Equal(at, delivery.CreatedAt);
    }

    [Fact]
    public void A_link_stores_the_certificate()
    {
        var interment = Guid.CreateVersion7();
        var certificate = Guid.CreateVersion7();
        var link = MaticaLink.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), interment, certificate);

        Assert.Equal(interment, link.IntermentId);
        Assert.Equal(certificate, link.DeathCertificateId);
    }

    [Fact]
    public void An_empty_certificate_is_refused()
    {
        var error = Assert.Throws<DomainRuleException>(() =>
            MaticaLink.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.Empty));

        Assert.Equal("matica.certificate_invalid", error.Code);
    }

    [Fact]
    public void The_processing_register_uses_stable_codes()
    {
        Assert.Equal(["holders", "payments", "documents", "audit"], GdprCatalog.Records.Select(record => record.Purpose));
        Assert.Equal(GdprExport.Requested, GdprExport.Open(Guid.CreateVersion7(), DateTimeOffset.UnixEpoch).Status);
    }

    [Fact]
    public void Occupancy_counts_open_full_and_closed_sites()
    {
        var count = CemeteryReports.Occupancy(
        [
            new ReportSite(false, true, 2, 1, false),
            new ReportSite(false, true, 1, 1, false),
            new ReportSite(true, true, 1, 0, false),
            new ReportSite(false, false, 1, 0, false),
            new ReportSite(false, true, 1, 0, true),
            new ReportSite(false, true, 1, 0, false),
        ]);

        Assert.Equal(6, count.SiteCount);
        Assert.Equal(1, count.OccupiedSites);
        Assert.Equal(1, count.FullSites);
        Assert.Equal(1, count.ClosedSites);
    }

    [Fact]
    public void Capacity_counts_free_and_reusable_positions()
    {
        var count = CemeteryReports.Capacity([PositionState.Free, PositionState.Occupied, PositionState.Reusable]);

        Assert.Equal(3, count.Capacity);
        Assert.Equal(1, count.FreePositions);
        Assert.Equal(1, count.ReusablePositions);
    }

    [Fact]
    public void A_rest_period_inside_the_year_is_listed()
    {
        var ends = CemeteryReports.EndWithinYear(new DateOnly(2011, 10, 15), Today, 15);

        Assert.Equal(Today, ends);
    }

    [Fact]
    public void A_rest_period_outside_the_year_is_omitted()
    {
        Assert.Null(CemeteryReports.EndWithinYear(new DateOnly(2010, 10, 14), Today, 15));
        Assert.Null(CemeteryReports.EndWithinYear(new DateOnly(2012, 10, 16), Today, 15));
    }

    [Fact]
    public void An_empty_interment_is_refused()
    {
        var error = Assert.Throws<DomainRuleException>(() =>
            MaticaLink.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.Empty, Guid.CreateVersion7()));

        Assert.Equal("matica.interment_invalid", error.Code);
    }

    [Fact]
    public void An_empty_secret_is_refused()
    {
        var error = Assert.Throws<DomainRuleException>(() =>
            WebhookSubscription.Register(Guid.CreateVersion7(), "https://example.com/hook", WebhookEvents.PaymentReceived, " "));

        Assert.Equal("webhook.secret_invalid", error.Code);
    }

    [Fact]
    public void A_blank_or_oversized_address_is_refused()
    {
        Assert.Equal("webhook.url_invalid", Assert.Throws<DomainRuleException>(() => WebhookAddress.Require("   ")).Code);
        Assert.Equal("webhook.url_invalid", Assert.Throws<DomainRuleException>(() => WebhookAddress.Require(new string('a', 201))).Code);
        Assert.Equal("webhook.url_invalid", Assert.Throws<DomainRuleException>(() => WebhookAddress.Require("not a url")).Code);
    }

    [Fact]
    public void An_empty_tenant_cannot_register_a_webhook_or_export()
    {
        Assert.Equal("tenant.required", Assert.Throws<DomainRuleException>(() => WebhookSubscription.Register(Guid.Empty, "https://example.com/hook", WebhookEvents.IntermentRecorded, "secret")).Code);
        Assert.Equal("webhook.event_invalid", Assert.Throws<DomainRuleException>(() => WebhookSubscription.Register(Guid.CreateVersion7(), "https://example.com/hook", null, "secret")).Code);
        Assert.Equal("webhook.secret_invalid", Assert.Throws<DomainRuleException>(() => WebhookSubscription.Register(Guid.CreateVersion7(), "https://example.com/hook", WebhookEvents.PaymentReceived, new string('k', 65))).Code);
        Assert.Equal("webhook.event_invalid", Assert.Throws<DomainRuleException>(() => WebhookDelivery.Queue(Guid.CreateVersion7(), Guid.CreateVersion7(), "nope", Guid.CreateVersion7(), DateTimeOffset.UnixEpoch)).Code);
        Assert.Equal("matica.not_found", Assert.Throws<DomainRuleException>(() => MaticaLink.Create(Guid.Empty, Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7())).Code);
        Assert.Equal("tenant.required", Assert.Throws<DomainRuleException>(() => GdprExport.Open(Guid.Empty, DateTimeOffset.UnixEpoch)).Code);
        Assert.Equal(0, CemeteryReports.Occupancy([]).SiteCount);
        Assert.Equal(0, CemeteryReports.Capacity([]).Capacity);
    }

    [Fact]
    public void A_delivery_without_a_subject_is_refused()
    {
        var error = Assert.Throws<DomainRuleException>(() =>
            WebhookDelivery.Queue(Guid.CreateVersion7(), Guid.CreateVersion7(), WebhookEvents.PaymentReceived, Guid.Empty, DateTimeOffset.UnixEpoch));

        Assert.Equal("webhook.not_found", error.Code);
    }

    [Fact]
    public void A_delivery_without_a_subscription_is_refused()
    {
        Assert.Equal("webhook.not_found", Assert.Throws<DomainRuleException>(() => WebhookDelivery.Queue(Guid.Empty, Guid.CreateVersion7(), WebhookEvents.PaymentReceived, Guid.CreateVersion7(), DateTimeOffset.UnixEpoch)).Code);
        Assert.Equal("webhook.not_found", Assert.Throws<DomainRuleException>(() => WebhookDelivery.Queue(Guid.CreateVersion7(), Guid.Empty, WebhookEvents.PaymentReceived, Guid.CreateVersion7(), DateTimeOffset.UnixEpoch)).Code);
        Assert.Equal("matica.not_found", Assert.Throws<DomainRuleException>(() => MaticaLink.Create(Guid.CreateVersion7(), Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7())).Code);
    }
}

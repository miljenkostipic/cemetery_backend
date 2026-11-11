using Cemetery.Domain.Billing;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Integrations;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Operations;
using Cemetery.Domain.Register;

namespace Cemetery.Domain.Tests;

public sealed class OutboxRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Recording_an_interment_raises_one_event()
    {
        var interment = Interment.Record(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), 1, IntermentKind.Coffin, new DateOnly(2026, 4, 1));

        var raised = Assert.Single(interment.DomainEvents);
        Assert.Equal("interment.recorded", raised.Type);
        Assert.Contains(interment.Id.ToString("D"), raised.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public void Scheduling_a_funeral_raises_one_event()
    {
        var funeral = Funeral.Schedule(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "2026-11-11T09:00:00", "2026-11-11T10:00:00", "chapel");

        var raised = Assert.Single(funeral.DomainEvents);
        Assert.Equal("funeral.scheduled", raised.Type);
    }

    [Fact]
    public void A_failing_message_waits_then_stops_after_five_tries()
    {
        var message = OutboxMessage.Capture(Guid.CreateVersion7(), new IntermentRecorded(Guid.CreateVersion7()), Now);

        Assert.True(message.IsDue(Now));
        message.BeginAttempt();
        message.MarkFailed("boom");
        Assert.Equal(1, message.Attempts);
        Assert.Equal("boom", message.LastError);
        Assert.False(message.IsDue(Now));
        Assert.True(message.IsDue(Now.AddSeconds(OutboxMessage.FirstRetrySeconds)));

        for (var attempt = 1; attempt < OutboxMessage.MaxAttempts; attempt++)
        {
            Assert.True(message.IsDue(Now.AddSeconds(OutboxMessage.DelaySeconds(message.Attempts))));
            message.BeginAttempt();
            message.MarkFailed("boom");
        }

        Assert.Equal(OutboxMessage.MaxAttempts, message.Attempts);
        Assert.False(message.IsDue(Now.AddDays(2)));
    }

    [Fact]
    public void A_processed_message_is_not_due()
    {
        var message = OutboxMessage.Capture(Guid.CreateVersion7(), new PaymentReceived(Guid.CreateVersion7()), Now);

        message.BeginAttempt();
        message.MarkProcessed(Now);

        Assert.NotNull(message.ProcessedAt);
        Assert.False(message.IsDue(Now.AddDays(1)));
    }

    [Fact]
    public void An_outbox_row_needs_a_tenant_and_a_json_payload()
    {
        var missing = Assert.Throws<DomainRuleException>(() => OutboxMessage.Capture(Guid.Empty, new IntermentRecorded(Guid.CreateVersion7()), Now));
        var blank = new BlankEvent();
        var payload = Assert.Throws<DomainRuleException>(() => OutboxMessage.Capture(Guid.CreateVersion7(), blank, Now));

        Assert.Equal("tenant.required", missing.Code);
        Assert.Equal("outbox.payload_invalid", payload.Code);
    }

    [Fact]
    public void A_webhook_signature_matches_only_the_same_body()
    {
        var signature = WebhookSignature.Sign("secret-ok", "100", """{"id":"1"}""");

        Assert.StartsWith("sha256=", signature, StringComparison.Ordinal);
        Assert.True(WebhookSignature.Matches("secret-ok", "100", """{"id":"1"}""", signature));
        Assert.False(WebhookSignature.Matches("secret-ok", "100", """{"id":"2"}""", signature));
        Assert.False(WebhookSignature.Matches("secret-ok", "100", """{"id":"1"}""", null));
    }

    private sealed record BlankEvent() : DomainEvent("interment.recorded", "not-json");
}

public sealed class CheckoutRulesTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();

    private static readonly Guid CemeteryId = Guid.CreateVersion7();

    [Fact]
    public void A_provider_callback_pays_a_charge_once()
    {
        var charge = Charge.Open(Tenant, Site(), Fee(), 2026);
        var payment = Payment.BeginCheckout(charge, "monri", new DateOnly(2026, 11, 11));
        payment.AttachCheckout("https://ipgtest.monri.com/pay/abc");

        Assert.True(payment.Accept("txn-1"));
        charge.Pay(payment.Id);
        Assert.False(payment.Accept("txn-1"));

        Assert.Equal(ChargeStatus.Paid, charge.Status);
        Assert.Equal(payment.Id, charge.LatestPaymentId);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal("txn-1", payment.ProviderTransactionId);
        Assert.Equal("payment.received", Assert.Single(payment.DomainEvents).Type);
    }

    [Fact]
    public void A_second_transaction_on_a_paid_checkout_is_refused()
    {
        var charge = Charge.Open(Tenant, Site(), Fee(), 2026);
        var payment = Payment.BeginCheckout(charge, "monri", new DateOnly(2026, 11, 11));
        Assert.True(payment.Accept("txn-1"));

        var error = Assert.Throws<DomainRuleException>(() => payment.Accept("txn-2"));

        Assert.Equal("payment.not_found", error.Code);
    }

    [Fact]
    public void Checkout_needs_an_https_redirect_and_an_open_charge()
    {
        var charge = Charge.Open(Tenant, Site(), Fee(), 2026);
        var payment = Payment.BeginCheckout(charge, "paypal", new DateOnly(2026, 11, 11));
        var redirect = Assert.Throws<DomainRuleException>(() => payment.AttachCheckout("http://pay.example/x"));
        _ = Payment.Record(charge, "cash", "25.00", new DateOnly(2026, 11, 11), null);
        var paid = Assert.Throws<DomainRuleException>(() => Payment.BeginCheckout(charge, "monri", new DateOnly(2026, 11, 11)));

        Assert.Equal(PaymentLink.Invalid, redirect.Code);
        Assert.Equal("charge.already_paid", paid.Code);
    }

    private static FeeSchedule Fee() => FeeSchedule.Add(Tenant, CemeteryId, "annual", "Upkeep", "25.00", "EUR");

    private static GraveSite Site() =>
        GraveSite.Place(Tenant, CemeteryId, Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Box());

    private static GeoPolygon Box() =>
        GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.001, 45),
            new GeoPoint(16.001, 45.001),
            new GeoPoint(16, 45.001),
            new GeoPoint(16, 45),
        ]);
}

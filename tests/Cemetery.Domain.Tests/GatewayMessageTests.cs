using System.Text.Json.Nodes;
using Cemetery.Domain.Billing;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Integrations;

namespace Cemetery.Domain.Tests;

public sealed class GatewayMessageTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly Guid PaymentId = Guid.CreateVersion7();
    private const string Secret = "secret-ok";

    [Fact]
    public void Monri_signs_a_new_payment_and_a_callback()
    {
        var draft = Draft();
        var body = MonriMessages.NewPayment(draft);
        var authorization = MonriMessages.Authorization("merchant-1", Secret, "1700000000", body);
        var session = MonriMessages.ReadSession("""{"payment_url":"https://ipgtest.monri.com/pay/abc","id":"sess-1"}""");
        var callback = Callback(PaymentOrder.Number(Tenant, PaymentId), "approved", "txn-1");
        var digest = MonriMessages.CallbackScheme + " " + MonriMessages.CallbackDigest(Secret, callback);

        var notice = MonriMessages.ReadCallback(callback, digest, Secret);

        Assert.StartsWith("WP3-v2 merchant-1 1700000000 ", authorization, StringComparison.Ordinal);
        Assert.Contains(PaymentOrder.Number(Tenant, PaymentId), body, StringComparison.Ordinal);
        Assert.Equal(2500, MonriMessages.Minor(25.00m));
        Assert.Equal("https://ipgtest.monri.com/pay/abc", session.RedirectUrl);
        Assert.True(notice.Approved);
        Assert.Equal(PaymentId, notice.PaymentId);
        Assert.Equal("txn-1", notice.TransactionId);
        Assert.Throws<DomainRuleException>(() => MonriMessages.ReadCallback(callback, "WP3-callback nope", Secret));
    }

    [Fact]
    public void PayPal_reads_the_approve_link_and_a_capture()
    {
        var body = PaypalMessages.NewOrder(Draft());
        var order = PaypalMessages.ReadOrder("""{"links":[{"rel":"approve","href":"https://www.sandbox.paypal.com/checkoutnow?token=abc"}]}""");
        var callback = new JsonObject
        {
            ["event_type"] = "PAYMENT.CAPTURE.COMPLETED",
            ["resource"] = new JsonObject
            {
                ["id"] = "txn-9",
                ["custom_id"] = PaymentOrder.Number(Tenant, PaymentId),
                ["status"] = "COMPLETED",
            },
        }.ToJsonString();
        var notice = PaypalMessages.ReadCallback(callback, PaypalMessages.Digest(Secret, callback), Secret);

        Assert.Contains("CAPTURE", body, StringComparison.Ordinal);
        Assert.Equal("https://www.sandbox.paypal.com/checkoutnow?token=abc", order.RedirectUrl);
        Assert.True(notice.Approved);
        Assert.Equal("txn-9", notice.TransactionId);
    }

    [Fact]
    public void Payrexx_reads_the_hosted_link_and_a_confirmed_transaction()
    {
        var body = PayrexxMessages.NewGateway(Draft());
        var session = PayrexxMessages.ReadGateway("""{"data":[{"id":1,"link":"https://demo.payrexx.com/pay/abc"}]}""");
        var callback = new JsonObject
        {
            ["transaction"] = new JsonObject
            {
                ["uuid"] = "txn-3",
                ["referenceId"] = PaymentOrder.Number(Tenant, PaymentId),
                ["status"] = "confirmed",
            },
        }.ToJsonString();
        var notice = PayrexxMessages.ReadCallback(callback, PayrexxMessages.Digest(Secret, callback), Secret);

        Assert.Contains("referenceId", body, StringComparison.Ordinal);
        Assert.Equal("https://demo.payrexx.com/pay/abc", session.RedirectUrl);
        Assert.True(notice.Approved);
        Assert.Equal("txn-3", notice.TransactionId);
    }

    [Fact]
    public void An_order_number_round_trips_the_tenant_and_the_payment()
    {
        var number = PaymentOrder.Number(Tenant, PaymentId);

        Assert.True(PaymentOrder.TryRead(number, out var tenantId, out var paymentId));
        Assert.Equal(Tenant, tenantId);
        Assert.Equal(PaymentId, paymentId);
        Assert.False(PaymentOrder.TryRead("short", out _, out _));
    }

    private static CheckoutDraft Draft() =>
        new(Tenant, PaymentId, 25.00m, "EUR", "Upkeep", "https://api.example.com/api/v1/payments/callbacks/monri");

    private static string Callback(string order, string status, string id)
    {
        var body = new JsonObject
        {
            ["order_number"] = order,
            ["status"] = status,
            ["id"] = id,
        };
        return body.ToJsonString();
    }
}

using System.Text.Json.Nodes;
using Cemetery.Application.Abstractions;
using Cemetery.Application.Billing;
using Cemetery.Domain.Billing;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Integrations;
using Cemetery.Domain.Layout;
using NSubstitute;

namespace Cemetery.Application.Tests;

public sealed class ProviderCallbackTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private const string Secret = "secret-ok";

    [Fact]
    public async Task A_sandbox_callback_marks_the_charge_paid_once()
    {
        var charge = Charge.Open(Tenant, Site(), Fee(), 2026);
        var payment = Payment.BeginCheckout(charge, "monri", new DateOnly(2026, 11, 11));
        payment.AttachCheckout("https://ipgtest.monri.com/pay/abc");
        var billing = Substitute.For<IBillingRepository>();
        var account = MerchantAccount.Connect(Tenant, "monri", new MerchantCredentials("merchant-1", Secret, true));
        billing.FindProviderAsync("monri", Arg.Any<CancellationToken>()).Returns(account);
        billing.FindPaymentAsync(payment.Id, Arg.Any<CancellationToken>()).Returns(payment);
        billing.FindChargeAsync(charge.Id, Arg.Any<CancellationToken>()).Returns(charge);
        var unit = Substitute.For<IUnitOfWork>();
        var tenant = new TenantHolder();
        var handler = new ApplyProviderCallbackHandler([new MonriDouble()], tenant, billing, unit);
        var payload = Callback(PaymentOrder.Number(Tenant, payment.Id), "approved", "txn-1");
        var signature = MonriMessages.CallbackScheme + " " + MonriMessages.CallbackDigest(Secret, payload);
        var command = new ApplyProviderCallback("monri", payload, signature);

        var first = await handler.Handle(command, CancellationToken.None);
        var second = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(payment.Id, first.Id);
        Assert.Equal(payment.Id, second.Id);
        Assert.Equal(ChargeStatus.Paid, charge.Status);
        Assert.Equal(payment.Id, charge.LatestPaymentId);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        await unit.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static string Callback(string order, string status, string id) =>
        new JsonObject { ["order_number"] = order, ["status"] = status, ["id"] = id }.ToJsonString();

    private static FeeSchedule Fee() => FeeSchedule.Add(Tenant, CemeteryId, "annual", "Upkeep", "25.00", "EUR");

    private static GraveSite Site() =>
        GraveSite.Place(Tenant, CemeteryId, Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Box());

    private static readonly Guid CemeteryId = Guid.CreateVersion7();

    private static GeoPolygon Box() =>
        GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.001, 45),
            new GeoPoint(16.001, 45.001),
            new GeoPoint(16, 45.001),
            new GeoPoint(16, 45),
        ]);

    private sealed class MonriDouble : IPaymentGateway
    {
        public string Name => BillingProviders.Monri;

        public string SignatureHeader => MonriMessages.SignatureHeader;

        public Task<HostedCheckout> OpenAsync(CheckoutDraft draft, MerchantCredentials account, CancellationToken cancellationToken) =>
            Task.FromResult(new HostedCheckout("https://ipgtest.monri.com/pay/abc"));

        public ProviderNotice Read(string payload, string? signature, string secret) =>
            MonriMessages.ReadCallback(payload, signature, secret);
    }

    private sealed class TenantHolder : ITenantContext
    {
        public Guid? OrganizationId { get; private set; }

        public void Use(Guid organizationId) => OrganizationId = organizationId;
    }
}

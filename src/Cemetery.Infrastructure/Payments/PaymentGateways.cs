using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Cemetery.Application.Abstractions;
using Cemetery.Domain.Billing;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Integrations;
using Microsoft.Extensions.Configuration;

namespace Cemetery.Infrastructure.Payments;

internal static class PaymentHosts
{
    public static Uri Read(IConfiguration configuration, string key, string fallback)
    {
        var raw = configuration[key];
        var text = string.IsNullOrWhiteSpace(raw) ? fallback : raw.Trim();
        return new Uri(text.TrimEnd('/') + "/");
    }
}

internal static class PaymentHttp
{
    public const string Client = "payments";

    public static async Task<string> SendAsync(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new DomainRuleException("payment.provider_invalid");

        return body;
    }
}

internal sealed class MonriGateway(IHttpClientFactory http, IConfiguration configuration, TimeProvider clock) : IPaymentGateway
{
    public string Name => BillingProviders.Monri;

    public string SignatureHeader => MonriMessages.SignatureHeader;

    public async Task<HostedCheckout> OpenAsync(CheckoutDraft draft, MerchantCredentials account, CancellationToken cancellationToken)
    {
        var body = MonriMessages.NewPayment(draft);
        var timestamp = clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        using var request = Request(body, account, timestamp);
        var json = await PaymentHttp.SendAsync(http.CreateClient(PaymentHttp.Client), request, cancellationToken).ConfigureAwait(false);
        return MonriMessages.ReadSession(json);
    }

    public ProviderNotice Read(string payload, string? signature, string secret) =>
        MonriMessages.ReadCallback(payload, signature, secret);

    private HttpRequestMessage Request(string body, MerchantCredentials account, string timestamp)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Host(), "v2/payment/new"))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation(SignatureHeader, MonriMessages.Authorization(account.MerchantId, account.Secret, timestamp, body));
        return request;
    }

    private Uri Host() => PaymentHosts.Read(configuration, "Payments:Monri", "https://ipgtest.monri.com");
}

internal sealed class PaypalGateway(IHttpClientFactory http, IConfiguration configuration) : IPaymentGateway
{
    public string Name => BillingProviders.Paypal;

    public string SignatureHeader => PaypalMessages.SignatureHeader;

    public async Task<HostedCheckout> OpenAsync(CheckoutDraft draft, MerchantCredentials account, CancellationToken cancellationToken)
    {
        var client = http.CreateClient(PaymentHttp.Client);
        var token = await Token(client, account, cancellationToken).ConfigureAwait(false);
        var json = await Order(client, draft, token, cancellationToken).ConfigureAwait(false);
        return PaypalMessages.ReadOrder(json);
    }

    public ProviderNotice Read(string payload, string? signature, string secret) =>
        PaypalMessages.ReadCallback(payload, signature, secret);

    private async Task<string> Token(HttpClient client, MerchantCredentials account, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Host(), "v1/oauth2/token"))
        {
            Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes(account.MerchantId + ":" + account.Secret));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        var json = await PaymentHttp.SendAsync(client, request, cancellationToken).ConfigureAwait(false);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new DomainRuleException("payment.provider_invalid");
    }

    private async Task<string> Order(HttpClient client, CheckoutDraft draft, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Host(), "v2/checkout/orders"))
        {
            Content = new StringContent(PaypalMessages.NewOrder(draft), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await PaymentHttp.SendAsync(client, request, cancellationToken).ConfigureAwait(false);
    }

    private Uri Host() => PaymentHosts.Read(configuration, "Payments:Paypal", "https://api-m.sandbox.paypal.com");
}

internal sealed class PayrexxGateway(IHttpClientFactory http, IConfiguration configuration) : IPaymentGateway
{
    public string Name => BillingProviders.Payrexx;

    public string SignatureHeader => PayrexxMessages.SignatureHeader;

    public async Task<HostedCheckout> OpenAsync(CheckoutDraft draft, MerchantCredentials account, CancellationToken cancellationToken)
    {
        var body = PayrexxMessages.NewGateway(draft);
        var path = "v1.0/Gateway/?instance=" + Uri.EscapeDataString(account.MerchantId);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(Host(), path))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation(SignatureHeader, PayrexxMessages.Digest(account.Secret, body));
        var json = await PaymentHttp.SendAsync(http.CreateClient(PaymentHttp.Client), request, cancellationToken).ConfigureAwait(false);
        return PayrexxMessages.ReadGateway(json);
    }

    public ProviderNotice Read(string payload, string? signature, string secret) =>
        PayrexxMessages.ReadCallback(payload, signature, secret);

    private Uri Host() => PaymentHosts.Read(configuration, "Payments:Payrexx", "https://api.payrexx.com");
}

internal sealed class ConfiguredPaymentLinks(IConfiguration configuration) : IPaymentLinks
{
    public string Callback(string provider)
    {
        var root = configuration["App:ApiUrl"];
        var text = string.IsNullOrWhiteSpace(root) ? "http://localhost:5080" : root.Trim().TrimEnd('/');
        return text + "/api/v1/payments/callbacks/" + provider;
    }
}

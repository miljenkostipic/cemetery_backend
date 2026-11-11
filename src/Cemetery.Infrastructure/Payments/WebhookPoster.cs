using System.Text;
using Cemetery.Application.Abstractions;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Integrations;
using Cemetery.Domain.Launch;

namespace Cemetery.Infrastructure.Payments;

internal sealed class WebhookPoster(IHttpClientFactory http, TimeProvider clock) : IWebhookPoster
{
    public const string Client = "webhooks";

    public async Task<int> PostAsync(string url, string secret, string body, CancellationToken cancellationToken)
    {
        var canonical = WebhookAddress.Require(url);
        var timestamp = WebhookSignature.Timestamp(clock.GetUtcNow());
        using var request = new HttpRequestMessage(HttpMethod.Post, canonical)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation(WebhookSignature.TimestampHeader, timestamp);
        request.Headers.TryAddWithoutValidation(WebhookSignature.Header, WebhookSignature.Sign(secret, timestamp, body));
        try
        {
            using var response = await http.CreateClient(Client).SendAsync(request, cancellationToken).ConfigureAwait(false);
            return (int)response.StatusCode;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DomainRuleException("webhook.timeout");
        }
    }
}

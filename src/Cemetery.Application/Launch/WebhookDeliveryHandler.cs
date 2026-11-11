using Cemetery.Application.Abstractions;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Integrations;
using Cemetery.Domain.Launch;

namespace Cemetery.Application.Launch;

public sealed class WebhookDeliveryHandler(
    IWebhookRepository webhooks,
    IWebhookPoster poster,
    TimeProvider clock) : IOutboxHandler
{
    public async Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (!WebhookEvents.IsKnown(message.Type))
            return;

        var body = WebhookEnvelope.Write(message);
        var matches = await webhooks.ListByEventAsync(message.Type, cancellationToken).ConfigureAwait(false);
        foreach (var subscription in matches)
            await Send(subscription, message, body, cancellationToken).ConfigureAwait(false);
    }

    private async Task Send(WebhookSubscription subscription, OutboxMessage message, string body, CancellationToken cancellationToken)
    {
        if (await webhooks.WasDeliveredAsync(subscription.Id, message.Id, cancellationToken).ConfigureAwait(false))
            return;

        var delivery = WebhookDelivery.Queue(subscription.TenantId, subscription.Id, message.Type, message.Id, clock.GetUtcNow());
        var status = await Post(subscription, body, delivery, cancellationToken).ConfigureAwait(false);
        webhooks.AddDelivery(delivery);
        if (status is < 200 or >= 300)
            throw new DomainRuleException("webhook.delivery_failed");
    }

    private async Task<int> Post(WebhookSubscription subscription, string body, WebhookDelivery delivery, CancellationToken cancellationToken)
    {
        try
        {
            var status = await poster.PostAsync(subscription.Url, subscription.Secret, body, cancellationToken).ConfigureAwait(false);
            var failed = status is < 200 or >= 300;
            delivery.Note(status, failed ? "webhook.delivery_failed" : null);
            return status;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            delivery.Note(null, exception.Message);
            webhooks.AddDelivery(delivery);
            throw;
        }
    }
}

using Cemetery.Domain.Identity;
using Cemetery.Domain.Integrations;

namespace Cemetery.Domain.Launch;

public sealed class WebhookSubscription : TenantEntity
{
    public const int SecretMaxLength = 64;

    private WebhookSubscription()
    {
        Url = "";
        Event = "";
        Secret = "";
    }

    public string Url { get; private set; }

    public string Event { get; private set; }

    public string Secret { get; private set; }

    public static WebhookSubscription Register(Guid tenantId, string? url, string? eventName, string? secret)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");

        var name = (eventName ?? "").Trim();
        if (!WebhookEvents.IsKnown(name))
            throw new DomainRuleException("webhook.event_invalid");

        var key = (secret ?? "").Trim();
        if (key.Length == 0 || key.Length > SecretMaxLength)
            throw new DomainRuleException("webhook.secret_invalid");

        return new WebhookSubscription
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Url = WebhookAddress.Require(url),
            Event = name,
            Secret = key,
        };
    }
}

public enum WebhookDeliveryStatus
{
    Pending = 1,
    Delivered = 2,
    Failed = 3,
}

public sealed class WebhookDelivery : TenantEntity
{
    private WebhookDelivery()
    {
        Event = "";
    }

    public Guid SubscriptionId { get; private set; }

    public string Event { get; private set; }

    public Guid SubjectId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public WebhookDeliveryStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public int? HttpStatus { get; private set; }

    public string? LastError { get; private set; }

    public static WebhookDelivery Queue(Guid tenantId, Guid subscriptionId, string eventName, Guid subjectId, DateTimeOffset at)
    {
        if (tenantId == Guid.Empty || subscriptionId == Guid.Empty || subjectId == Guid.Empty)
            throw new DomainRuleException("webhook.not_found");
        if (!WebhookEvents.IsKnown(eventName))
            throw new DomainRuleException("webhook.event_invalid");

        return new WebhookDelivery
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            SubscriptionId = subscriptionId,
            Event = eventName,
            SubjectId = subjectId,
            CreatedAt = at,
            Status = WebhookDeliveryStatus.Pending,
        };
    }

    public void Note(int? statusCode, string? error)
    {
        Attempts++;
        HttpStatus = statusCode;
        LastError = Clip(error);
        Status = error is null ? WebhookDeliveryStatus.Delivered : WebhookDeliveryStatus.Failed;
    }

    private static string? Clip(string? error)
    {
        if (error is null)
            return null;

        var text = error.Trim();
        if (text.Length > OutboxMessage.ErrorMaxLength)
            text = text[..OutboxMessage.ErrorMaxLength];

        return text.Length == 0 ? "webhook.delivery_failed" : text;
    }
}

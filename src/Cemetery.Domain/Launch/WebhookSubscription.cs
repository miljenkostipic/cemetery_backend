using Cemetery.Domain.Identity;

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

public sealed class WebhookDelivery : TenantEntity
{
    public const string Pending = "pending";

    private WebhookDelivery()
    {
        Event = "";
        Status = "";
    }

    public Guid SubscriptionId { get; private set; }

    public string Event { get; private set; }

    public Guid SubjectId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string Status { get; private set; }

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
            Status = Pending,
        };
    }
}

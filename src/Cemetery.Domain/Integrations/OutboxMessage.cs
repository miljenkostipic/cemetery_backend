using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Integrations;

public sealed class OutboxMessage : TenantEntity
{
    public const int TypeMaxLength = 64;

    public const int ErrorMaxLength = 500;

    public const int MaxAttempts = 5;

    public const int FirstRetrySeconds = 60;

    public const int SecondRetrySeconds = 300;

    public const int ThirdRetrySeconds = 900;

    public const int FourthRetrySeconds = 3600;

    private OutboxMessage()
    {
        Type = "";
        Payload = "";
    }

    public string Type { get; private set; }

    public string Payload { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Capture(Guid tenantId, DomainEvent domainEvent, DateTimeOffset occurredAt)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");
        if (domainEvent.Type.Length == 0 || domainEvent.Type.Length > TypeMaxLength || !domainEvent.Payload.StartsWith('{'))
            throw new DomainRuleException("outbox.payload_invalid");

        return new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Type = domainEvent.Type,
            Payload = domainEvent.Payload,
            OccurredAt = occurredAt,
        };
    }

    public bool IsDue(DateTimeOffset now)
    {
        if (ProcessedAt is not null || Attempts >= MaxAttempts)
            return false;
        if (Attempts == 0)
            return true;

        return now >= OccurredAt.AddSeconds(DelaySeconds(Attempts));
    }

    public void BeginAttempt() => Attempts++;

    public void MarkProcessed(DateTimeOffset at) => ProcessedAt = at;

    public void MarkFailed(string error) => LastError = Clip(error);

    public static int DelaySeconds(int attempts) => attempts switch
    {
        <= 1 => FirstRetrySeconds,
        2 => SecondRetrySeconds,
        3 => ThirdRetrySeconds,
        _ => FourthRetrySeconds,
    };

    private static string Clip(string error)
    {
        var text = error.Trim();
        if (text.Length == 0)
            text = "outbox.failed";
        if (text.Length > ErrorMaxLength)
            text = text[..ErrorMaxLength];

        return text;
    }
}

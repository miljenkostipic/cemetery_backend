using Cemetery.Domain.Identity;
using Cemetery.Domain.Launch;

namespace Cemetery.Domain.Integrations;

public sealed record IntermentRecorded(Guid IntermentId) : DomainEvent(
    WebhookEvents.IntermentRecorded,
    $$"""{"intermentId":"{{IntermentId:D}}"}""");

public sealed record PaymentReceived(Guid PaymentId) : DomainEvent(
    WebhookEvents.PaymentReceived,
    $$"""{"paymentId":"{{PaymentId:D}}"}""");

public sealed record FuneralScheduled(Guid FuneralId) : DomainEvent(
    WebhookEvents.FuneralScheduled,
    $$"""{"funeralId":"{{FuneralId:D}}"}""");

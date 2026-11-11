using Cemetery.Domain.Billing;
using Cemetery.Domain.Integrations;

namespace Cemetery.Application.Abstractions;

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxMessage>> ListDueAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}

public interface IOutboxHandler
{
    Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken);
}

public interface IWebhookPoster
{
    Task<int> PostAsync(string url, string secret, string body, CancellationToken cancellationToken);
}

public interface IPaymentGateway
{
    string Name { get; }

    string SignatureHeader { get; }

    Task<HostedCheckout> OpenAsync(CheckoutDraft draft, MerchantCredentials account, CancellationToken cancellationToken);

    ProviderNotice Read(string payload, string? signature, string secret);
}

public interface IPaymentLinks
{
    string Callback(string provider);
}

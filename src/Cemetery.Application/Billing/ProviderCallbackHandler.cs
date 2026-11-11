using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Billing;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Integrations;
using FluentValidation;

namespace Cemetery.Application.Billing;

public sealed record ApplyProviderCallback(string Provider, string Payload, string? Signature);

public sealed class ApplyProviderCallbackValidator : AbstractValidator<ApplyProviderCallback>
{
    public ApplyProviderCallbackValidator()
    {
        RuleFor(command => command.Provider).Must(BillingProviders.IsKnown).WithErrorCode("payment.provider_invalid");
        RuleFor(command => command.Payload).NotEmpty().WithErrorCode("payment.not_found");
    }
}

public sealed class ApplyProviderCallbackHandler(
    IEnumerable<IPaymentGateway> gateways,
    ITenantContext tenant,
    IBillingRepository billing,
    IUnitOfWork unitOfWork) : ICommandHandler<ApplyProviderCallback, PaymentView>
{
    public async Task<PaymentView> Handle(ApplyProviderCallback command, CancellationToken cancellationToken)
    {
        var gateway = Require(command.Provider);
        var noticed = gateway.Read(command.Payload, command.Signature, await Secret(command.Provider, command.Payload, cancellationToken).ConfigureAwait(false));
        if (noticed.TenantId != tenant.OrganizationId)
            throw new DomainRuleException("payment.signature_invalid");

        return await Apply(noticed, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> Secret(string provider, string payload, CancellationToken cancellationToken)
    {
        if (!Peek(payload, out var tenantId))
            throw new DomainRuleException("payment.not_found");

        tenant.Use(tenantId);
        var account = await billing.FindProviderAsync(provider, cancellationToken).ConfigureAwait(false);
        if (account is null || !account.Enabled)
            throw new NotFoundException("payment.provider_invalid");

        return account.Secret;
    }

    private IPaymentGateway Require(string provider) =>
        gateways.FirstOrDefault(item => item.Name == provider) ?? throw new DomainRuleException("payment.provider_invalid");

    private static bool Peek(string payload, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var order = OrderOf(payload);
        return PaymentOrder.TryRead(order, out tenantId, out _);
    }

    private static string? OrderOf(string payload)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("order_number", out var order))
                return order.GetString();
            if (document.RootElement.TryGetProperty("resource", out var resource) && resource.TryGetProperty("custom_id", out var custom))
                return custom.GetString();
            if (document.RootElement.TryGetProperty("transaction", out var transaction) && transaction.TryGetProperty("referenceId", out var reference))
                return reference.GetString();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }

        return null;
    }

    private async Task<PaymentView> Apply(ProviderNotice notice, CancellationToken cancellationToken)
    {
        if (!notice.Approved)
            throw new DomainRuleException("payment.declined");

        var payment = await billing.FindPaymentAsync(notice.PaymentId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("payment.not_found");
        if (!payment.Accept(notice.TransactionId))
            return BillingMaps.Payment(payment);

        var charge = await billing.FindChargeAsync(payment.ChargeId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("charge.not_found");
        charge.Pay(payment.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BillingMaps.Payment(payment);
    }
}

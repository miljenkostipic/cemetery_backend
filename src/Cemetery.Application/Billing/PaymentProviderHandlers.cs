using Cemetery.Application.Abstractions;
using Cemetery.Application.Register;
using Cemetery.Domain.Billing;
using FluentValidation;

namespace Cemetery.Application.Billing;

public sealed record ListPaymentProviders;

public sealed class ListPaymentProvidersHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IBillingRepository billing) : IQueryHandler<ListPaymentProviders, IReadOnlyList<PaymentProviderView>>
{
    public async Task<IReadOnlyList<PaymentProviderView>> Handle(ListPaymentProviders query, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var saved = await billing.ListProvidersAsync(cancellationToken).ConfigureAwait(false);
        return BillingProviders.All.Select(name => Map(name, saved)).ToArray();
    }

    private static PaymentProviderView Map(string name, IReadOnlyList<MerchantAccount> saved)
    {
        var account = saved.FirstOrDefault(item => item.Provider == name);
        if (account is null)
            return BillingMaps.Missing(name);

        return BillingMaps.Provider(account);
    }
}

public sealed record SavePaymentProvider(string Provider, string MerchantId, string Secret, bool Enabled);

public sealed class SavePaymentProviderValidator : AbstractValidator<SavePaymentProvider>
{
    public SavePaymentProviderValidator()
    {
        RuleFor(command => command.Provider).Must(BillingProviders.IsKnown).WithErrorCode("payment.provider_invalid");
        RuleFor(command => command.MerchantId).Must(MerchantCode.Accepts).WithErrorCode(MerchantCode.Invalid);
        RuleFor(command => command.Secret).Must(ProviderSecret.Accepts).WithErrorCode(ProviderSecret.Invalid);
    }
}

public sealed class SavePaymentProviderHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    IBillingRepository billing,
    IUnitOfWork unitOfWork) : ICommandHandler<SavePaymentProvider, PaymentProviderView>
{
    public async Task<PaymentProviderView> Handle(SavePaymentProvider command, CancellationToken cancellationToken)
    {
        var organizationId = await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var account = await billing.FindProviderAsync(command.Provider, cancellationToken).ConfigureAwait(false);
        var saved = await StoreAsync(organizationId, command, account, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BillingMaps.Provider(saved);
    }

    private async Task<MerchantAccount> StoreAsync(
        Guid organizationId,
        SavePaymentProvider command,
        MerchantAccount? account,
        CancellationToken cancellationToken)
    {
        var credentials = new MerchantCredentials(command.MerchantId, command.Secret, command.Enabled);
        if (account is null)
            return await AddAsync(organizationId, command.Provider, credentials, cancellationToken).ConfigureAwait(false);

        account.Replace(credentials);
        return account;
    }

    private async Task<MerchantAccount> AddAsync(
        Guid organizationId,
        string provider,
        MerchantCredentials credentials,
        CancellationToken cancellationToken)
    {
        var created = MerchantAccount.Connect(organizationId, provider, credentials);
        await billing.AddProviderAsync(created, cancellationToken).ConfigureAwait(false);
        return created;
    }
}

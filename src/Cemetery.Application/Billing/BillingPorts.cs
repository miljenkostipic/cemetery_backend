using Cemetery.Domain.Billing;

namespace Cemetery.Application.Abstractions;

public interface IBillingRepository
{
    Task<IReadOnlyList<FeeSchedule>> ListFeesAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task AddFeeAsync(FeeSchedule fee, CancellationToken cancellationToken);

    Task<FeeSchedule?> FindFeeAsync(Guid feeScheduleId, CancellationToken cancellationToken);

    Task<bool> FeeIsChargedAsync(Guid feeScheduleId, CancellationToken cancellationToken);

    void RemoveFee(FeeSchedule fee);

    Task<IReadOnlyList<Charge>> ListChargesAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Charge>> ListOpenChargesAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task<Charge?> FindChargeAsync(Guid chargeId, CancellationToken cancellationToken);

    Task<Payment?> FindPaymentAsync(Guid paymentId, CancellationToken cancellationToken);

    void AddPayment(Payment payment);

    Task<IReadOnlyList<Charge>> ListArrearsAsync(Guid cemeteryId, CancellationToken cancellationToken);

    Task AddChargesAsync(IReadOnlyList<Charge> charges, CancellationToken cancellationToken);

    Task<IReadOnlySet<ChargeKey>> ChargedPairsAsync(Guid cemeteryId, int year, CancellationToken cancellationToken);

    Task<IReadOnlyList<MerchantAccount>> ListProvidersAsync(CancellationToken cancellationToken);

    Task<MerchantAccount?> FindProviderAsync(string provider, CancellationToken cancellationToken);

    Task AddProviderAsync(MerchantAccount account, CancellationToken cancellationToken);
}

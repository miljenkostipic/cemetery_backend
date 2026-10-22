using Cemetery.Application.Abstractions;
using Cemetery.Domain.Billing;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Infrastructure.Persistence;

internal sealed class BillingRepository(CemeteryDbContext db) : IBillingRepository
{
    public async Task<IReadOnlyList<FeeSchedule>> ListFeesAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.FeeSchedules.AsNoTracking()
            .Where(fee => fee.CemeteryId == cemeteryId)
            .OrderBy(fee => fee.Name)
            .ThenBy(fee => fee.Kind)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task AddFeeAsync(FeeSchedule fee, CancellationToken cancellationToken)
    {
        db.FeeSchedules.Add(fee);
        return Task.CompletedTask;
    }

    public Task<FeeSchedule?> FindFeeAsync(Guid feeScheduleId, CancellationToken cancellationToken) =>
        db.FeeSchedules.FirstOrDefaultAsync(fee => fee.Id == feeScheduleId, cancellationToken);

    public Task<bool> FeeIsChargedAsync(Guid feeScheduleId, CancellationToken cancellationToken) =>
        db.Charges.AnyAsync(charge => charge.FeeScheduleId == feeScheduleId, cancellationToken);

    public void RemoveFee(FeeSchedule fee) => db.FeeSchedules.Remove(fee);

    public async Task<IReadOnlyList<Charge>> ListOpenChargesAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.Charges
            .Where(charge => charge.CemeteryId == cemeteryId && charge.Status == ChargeStatuses.Open)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<Charge?> FindChargeAsync(Guid chargeId, CancellationToken cancellationToken) =>
        db.Charges.FirstOrDefaultAsync(charge => charge.Id == chargeId, cancellationToken);

    public Task<Payment?> FindPaymentAsync(Guid paymentId, CancellationToken cancellationToken) =>
        db.Payments.FirstOrDefaultAsync(payment => payment.Id == paymentId, cancellationToken);

    public void AddPayment(Payment payment) => db.Payments.Add(payment);

    public async Task<IReadOnlyList<Charge>> ListChargesAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.Charges.AsNoTracking()
            .Where(charge => charge.CemeteryId == cemeteryId)
            .OrderBy(charge => charge.GraveSiteCode)
            .ThenBy(charge => charge.Year)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Charge>> ListArrearsAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.Charges.AsNoTracking()
            .Where(charge => charge.CemeteryId == cemeteryId && (charge.Status == ChargeStatuses.Open || charge.Status == ChargeStatuses.Reminded))
            .OrderBy(charge => charge.GraveSiteCode)
            .ThenBy(charge => charge.Year)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task AddChargesAsync(IReadOnlyList<Charge> charges, CancellationToken cancellationToken)
    {
        db.Charges.AddRange(charges);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlySet<ChargeKey>> ChargedPairsAsync(Guid cemeteryId, int year, CancellationToken cancellationToken)
    {
        var rows = await db.Charges.AsNoTracking()
            .Where(charge => charge.CemeteryId == cemeteryId && charge.Year == year)
            .Select(charge => new { charge.GraveSiteId, charge.FeeScheduleId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(row => new ChargeKey(row.GraveSiteId, row.FeeScheduleId)).ToHashSet();
    }

    public async Task<IReadOnlyList<MerchantAccount>> ListProvidersAsync(CancellationToken cancellationToken) =>
        await db.MerchantAccounts.AsNoTracking().OrderBy(account => account.Provider).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<MerchantAccount?> FindProviderAsync(string provider, CancellationToken cancellationToken) =>
        db.MerchantAccounts.FirstOrDefaultAsync(account => account.Provider == provider, cancellationToken);

    public Task AddProviderAsync(MerchantAccount account, CancellationToken cancellationToken)
    {
        db.MerchantAccounts.Add(account);
        return Task.CompletedTask;
    }
}

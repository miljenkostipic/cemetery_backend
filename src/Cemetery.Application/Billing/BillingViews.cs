using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Billing;

namespace Cemetery.Application.Billing;

public sealed record FeeScheduleView(Guid Id, Guid CemeteryId, FeeKind Kind, string Name, string Amount, string Currency);

public sealed record YearlyRunView(int Year, int ChargeCount);

public sealed record ReminderRunView(int RemindedCount);

public sealed record ChargeView(
    Guid Id,
    Guid CemeteryId,
    Guid GraveSiteId,
    string GraveSiteCode,
    string HolderName,
    FeeKind Kind,
    string Description,
    string Amount,
    string Currency,
    ChargeStatus Status,
    DateOnly DueOn,
    int? Year,
    Guid? LatestPaymentId);

public sealed record ArrearsRow(
    Guid ChargeId,
    string GraveSiteCode,
    string HolderName,
    string Amount,
    string Currency,
    DateOnly DueOn,
    ChargeStatus Status);

public sealed record PaymentProviderView(BillingProvider Provider, bool Enabled, bool Connected, string? MerchantId);

public sealed record FeeScheduleBody(string Kind, string Name, string Amount, string Currency);

public sealed record YearlyRunBody(int Year);

public sealed record ManualPaymentBody(string Method, string Amount, DateOnly PaidOn, string? Reference);

public sealed record BillingReasonBody(string Reason);

public sealed record PaymentView(
    Guid Id,
    Guid ChargeId,
    ManualMethod Method,
    string Amount,
    string Currency,
    PaymentStatus Status,
    DateOnly? PaidOn,
    string? RedirectUrl,
    string? ReceiptNumber);

public sealed record ReceiptView(
    Guid PaymentId,
    string ReceiptNumber,
    string Amount,
    string Currency,
    DateOnly PaidOn,
    ManualMethod Method,
    string Description);

public sealed record PaymentProviderBody(string MerchantId, string Secret, bool Enabled);

internal static class BillingMaps
{
    public static FeeScheduleView Fee(FeeSchedule fee) =>
        new(fee.Id, fee.CemeteryId, fee.Kind, fee.Name, FeeAmount.Text(fee.Amount), fee.Currency);

    public static ChargeView Charge(Charge charge) =>
        new(
            charge.Id,
            charge.CemeteryId,
            charge.GraveSiteId,
            charge.GraveSiteCode,
            charge.HolderName,
            charge.Kind,
            charge.Description,
            FeeAmount.Text(charge.Amount),
            charge.Currency,
            charge.Status,
            charge.DueOn,
            charge.Year,
            charge.LatestPaymentId);

    public static PaymentView Payment(Payment payment) =>
        new(payment.Id, payment.ChargeId, payment.Method, FeeAmount.Text(payment.Amount), payment.Currency, payment.Status, payment.PaidOn, null, payment.ReceiptNumber);

    public static ReceiptView Receipt(Payment payment) =>
        new(payment.Id, payment.ReceiptNumber, FeeAmount.Text(payment.Amount), payment.Currency, payment.PaidOn, payment.Method, payment.Description);

    public static ArrearsRow Arrears(Charge charge) =>
        new(charge.Id, charge.GraveSiteCode, charge.HolderName, FeeAmount.Text(charge.Amount), charge.Currency, charge.DueOn, charge.Status);

    public static PaymentProviderView Provider(MerchantAccount account) =>
        new(account.Provider, account.Enabled, true, account.MerchantId);

    public static PaymentProviderView Missing(string provider) => new(BillingProviders.Require(provider), false, false, null);
}

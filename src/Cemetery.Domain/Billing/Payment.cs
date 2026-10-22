using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Billing;

public static class ManualMethods
{
    public const string Cash = "cash";

    public const string Bank = "bank";

    public const int MaxLength = 16;

    public static bool IsKnown(string? method) => method is Cash or Bank;

    public static string Require(string? method)
    {
        if (!IsKnown(method))
            throw new DomainRuleException("payment.method_invalid");

        return method!;
    }
}

public static class PaymentStatuses
{
    public const string Succeeded = "succeeded";

    public const string Voided = "voided";

    public const int MaxLength = 16;
}

public static class PaymentDate
{
    public const string Invalid = "payment.date_invalid";

    public static DateOnly Require(DateOnly paidOn)
    {
        if (paidOn.Year < BillingYear.First || paidOn.Year > BillingYear.Last)
            throw new DomainRuleException(Invalid);

        return paidOn;
    }

    public static bool Accepts(DateOnly paidOn) => RuleCheck.Accepts(() => Require(paidOn));
}

public static class PaymentAmount
{
    public const string Invalid = "payment.amount_invalid";

    public static decimal RequireMatch(string? raw, decimal expected)
    {
        if (!FeeAmount.Accepts(raw) || FeeAmount.Text(expected) != raw)
            throw new DomainRuleException(Invalid);

        return FeeAmount.Require(raw);
    }
}

public static class PaymentReference
{
    public const int MaxLength = 80;

    public const string Invalid = "payment.reference_invalid";

    public static string Require(string? raw)
    {
        var text = raw?.Trim() ?? "";
        if (text.Length > MaxLength || BillingText.Unsafe(text))
            throw new DomainRuleException(Invalid);

        return text;
    }

    public static bool Accepts(string? raw) => RuleCheck.Accepts(() => Require(raw));
}

public static class BillingReason
{
    public const int MinLength = 3;

    public const int MaxLength = 200;

    public static string Require(string? raw, string code)
    {
        var text = raw?.Trim() ?? "";
        if (text.Length < MinLength || text.Length > MaxLength || BillingText.Unsafe(text))
            throw new DomainRuleException(code);

        return text;
    }

    public static bool Accepts(string? raw) => RuleCheck.Accepts(() => Require(raw, "payment.reason_invalid"));
}

public static class BillingText
{
    public static bool Unsafe(string text)
    {
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@')
            return true;

        foreach (var character in text)
            if (character < ' ' || character == '\u007f')
                return true;

        return false;
    }
}

public sealed class Payment : TenantEntity
{
    public const int ReceiptMaxLength = 16;

    public const int DescriptionMaxLength = FeeName.MaxLength;

    private Payment()
    {
        Method = "";
        Currency = "";
        Status = "";
        Reference = "";
        ReceiptNumber = "";
        Description = "";
    }

    public Guid ChargeId { get; private set; }

    public string Method { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public string Status { get; private set; }

    public DateOnly PaidOn { get; private set; }

    public string Reference { get; private set; }

    public string ReceiptNumber { get; private set; }

    public string Description { get; private set; }

    public string? VoidReason { get; private set; }

    public static Payment Record(Charge charge, string? method, string? amount, DateOnly paidOn, string? reference)
    {
        var payment = Create(charge, ManualMethods.Require(method), PaymentAmount.RequireMatch(amount, charge.Amount), PaymentDate.Require(paidOn), PaymentReference.Require(reference));
        charge.Pay(payment.Id);
        return payment;
    }

    public void Void(string? reason)
    {
        if (Status != PaymentStatuses.Succeeded || !ManualMethods.IsKnown(Method))
            throw new DomainRuleException("payment.not_voidable");

        VoidReason = BillingReason.Require(reason, "payment.reason_invalid");
        Status = PaymentStatuses.Voided;
    }

    private static Payment Create(Charge charge, string method, decimal amount, DateOnly paidOn, string reference)
    {
        var payment = new Payment
        {
            Id = Guid.CreateVersion7(),
            TenantId = charge.TenantId,
            ChargeId = charge.Id,
            Method = method,
            Amount = amount,
            Currency = charge.Currency,
            Status = PaymentStatuses.Succeeded,
            PaidOn = paidOn,
            Reference = reference,
            Description = charge.Description,
        };
        payment.ReceiptNumber = Receipt(payment.Id);
        return payment;
    }

    private static string Receipt(Guid paymentId) => $"R-{paymentId.ToString("N")[..8].ToUpperInvariant()}";
}

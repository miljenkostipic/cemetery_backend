using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Integrations;

public static class PaymentOrder
{
    public const int Length = 64;

    public static string Number(Guid tenantId, Guid paymentId) =>
        string.Concat(tenantId.ToString("N"), paymentId.ToString("N"));

    public static bool TryRead(string? raw, out Guid tenantId, out Guid paymentId)
    {
        tenantId = Guid.Empty;
        paymentId = Guid.Empty;
        if (raw is null || raw.Length != Length)
            return false;

        return Guid.TryParseExact(raw[..32], "N", out tenantId)
            && Guid.TryParseExact(raw[32..], "N", out paymentId)
            && tenantId != Guid.Empty
            && paymentId != Guid.Empty;
    }
}

public static class PaymentLink
{
    public const int MaxLength = 500;

    public const string Invalid = "payment.redirect_invalid";

    public static string Require(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0 || text.Length > MaxLength)
            throw new DomainRuleException(Invalid);
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw new DomainRuleException(Invalid);

        return uri.AbsoluteUri;
    }
}

public readonly record struct CheckoutDraft(
    Guid TenantId,
    Guid PaymentId,
    decimal Amount,
    string Currency,
    string Description,
    string CallbackUrl);

public readonly record struct HostedCheckout(string RedirectUrl);

public readonly record struct ProviderNotice(Guid TenantId, Guid PaymentId, string TransactionId, bool Approved);

using System.Text.RegularExpressions;
using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Billing;

public static class BillingProviders
{
    public const string Monri = "monri";

    public const string Paypal = "paypal";

    public const string Payrexx = "payrexx";

    public const int MaxLength = 16;

    public static readonly string[] All = [Monri, Paypal, Payrexx];

    public static bool IsKnown(string? provider) => provider is Monri or Paypal or Payrexx;

    public static string Require(string? provider)
    {
        if (!IsKnown(provider))
            throw new DomainRuleException("payment.provider_invalid");

        return provider!;
    }
}

public readonly record struct MerchantCredentials(string MerchantId, string Secret, bool Enabled);

public static partial class MerchantCode
{
    public const int MaxLength = 64;

    public const string Invalid = "payment.merchant_invalid";

    public static string Require(string? raw)
    {
        if (raw is null || !Pattern().IsMatch(raw))
            throw new DomainRuleException(Invalid);

        return raw;
    }

    public static bool Accepts(string? raw) => RuleCheck.Accepts(() => Require(raw));

    [GeneratedRegex(@"^[A-Za-z0-9._-]{2,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

public static class ProviderSecret
{
    public const int MinLength = 8;

    public const int MaxLength = 200;

    public const string Invalid = "payment.secret_invalid";

    public static string Require(string? raw)
    {
        if (raw is null)
            throw new DomainRuleException(Invalid);
        if (raw.Length is < MinLength or > MaxLength)
            throw new DomainRuleException(Invalid);
        if (raw != raw.Trim())
            throw new DomainRuleException(Invalid);
        if (raw.Contains('\r') || raw.Contains('\n'))
            throw new DomainRuleException(Invalid);

        return raw;
    }

    public static bool Accepts(string? raw) => RuleCheck.Accepts(() => Require(raw));
}

public sealed class MerchantAccount : TenantEntity
{
    private MerchantAccount()
    {
        Provider = "";
        MerchantId = "";
        Secret = "";
    }

    public string Provider { get; private set; }

    public string MerchantId { get; private set; }

    public string Secret { get; private set; }

    public bool Enabled { get; private set; }

    public static MerchantAccount Connect(Guid tenantId, string? provider, MerchantCredentials credentials)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");

        return Create(tenantId, BillingProviders.Require(provider), credentials);
    }

    public void Replace(MerchantCredentials credentials)
    {
        MerchantId = MerchantCode.Require(credentials.MerchantId);
        Secret = ProviderSecret.Require(credentials.Secret);
        Enabled = credentials.Enabled;
    }

    private static MerchantAccount Create(Guid tenantId, string provider, MerchantCredentials credentials) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Provider = provider,
            MerchantId = MerchantCode.Require(credentials.MerchantId),
            Secret = ProviderSecret.Require(credentials.Secret),
            Enabled = credentials.Enabled,
        };
}

internal static class RuleCheck
{
    public static bool Accepts(Action check)
    {
        try
        {
            check();
            return true;
        }
        catch (DomainRuleException)
        {
            return false;
        }
    }
}

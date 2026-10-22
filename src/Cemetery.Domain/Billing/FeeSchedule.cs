using System.Globalization;
using System.Text.RegularExpressions;
using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Billing;

public static class FeeKinds
{
    public const string Annual = "annual";

    public const string Service = "service";

    public const int MaxLength = 16;

    public static bool IsKnown(string? kind) => kind is Annual or Service;
}

public static class FeeCurrencies
{
    public const string Euro = "EUR";

    public const string Franc = "CHF";

    public const string Mark = "BAM";

    public const int MaxLength = 3;

    public static bool IsKnown(string? currency) => currency is Euro or Franc or Mark;
}

public static partial class FeeName
{
    public const int MaxLength = 80;

    public const string Invalid = "fee.name_invalid";

    private const char Delete = '\u007f';

    public static string Require(string? raw)
    {
        if (raw is null || HasControl(raw))
            throw new DomainRuleException(Invalid);

        var name = raw.Trim();
        if (name.Length is < 1 or > MaxLength)
            throw new DomainRuleException(Invalid);

        return name;
    }

    public static bool Accepts(string? raw) => RuleCheck.Accepts(() => Require(raw));

    private static bool HasControl(string value)
    {
        foreach (var character in value)
        {
            if (character < ' ')
                return true;
            if (character == Delete)
                return true;
        }

        return false;
    }
}

public static partial class FeeAmount
{
    public const int Precision = 8;

    public const int Scale = 2;

    public const string Invalid = "fee.amount_invalid";

    private const string Zero = "0.00";

    public static decimal Require(string? raw)
    {
        if (!Matches(raw))
            throw new DomainRuleException(Invalid);

        return decimal.Parse(raw!, CultureInfo.InvariantCulture);
    }

    public static bool Accepts(string? raw) => RuleCheck.Accepts(() => Require(raw));

    public static string Text(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static bool Matches(string? raw)
    {
        if (raw is null)
            return false;
        if (!Pattern().IsMatch(raw))
            return false;

        return raw != Zero;
    }

    [GeneratedRegex(@"^(?:0|[1-9]\d{0,5})\.\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}

public sealed class FeeSchedule : TenantEntity
{
    private FeeSchedule()
    {
        Kind = "";
        Name = "";
        Currency = "";
    }

    public Guid CemeteryId { get; private set; }

    public string Kind { get; private set; }

    public string Name { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; }

    public static FeeSchedule Add(Guid tenantId, Guid cemeteryId, string? kind, string? name, string? amount, string? currency)
    {
        RequirePlace(tenantId, cemeteryId);
        return Create(tenantId, cemeteryId, RequireKind(kind), FeeName.Require(name), FeeAmount.Require(amount), RequireCurrency(currency));
    }

    private static void RequirePlace(Guid tenantId, Guid cemeteryId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");
        if (cemeteryId == Guid.Empty)
            throw new DomainRuleException("cemetery.not_found");
    }

    private static string RequireKind(string? kind)
    {
        if (!FeeKinds.IsKnown(kind))
            throw new DomainRuleException("fee.kind_invalid");

        return kind!;
    }

    private static string RequireCurrency(string? currency)
    {
        if (!FeeCurrencies.IsKnown(currency))
            throw new DomainRuleException("fee.currency_invalid");

        return currency!;
    }

    private static FeeSchedule Create(Guid tenantId, Guid cemeteryId, string kind, string name, decimal amount, string currency) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = cemeteryId,
            Kind = kind,
            Name = name,
            Amount = amount,
            Currency = currency,
        };
}

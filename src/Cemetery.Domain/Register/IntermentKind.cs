using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;

namespace Cemetery.Domain.Register;

public enum IntermentKind
{
    Coffin = 1,
    Urn = 2,
}

public static class IntermentKinds
{
    public const string Coffin = "coffin";
    public const string Urn = "urn";

    public static string ToCode(IntermentKind kind) => kind switch
    {
        IntermentKind.Coffin => Coffin,
        IntermentKind.Urn => Urn,
        _ => throw new DomainRuleException("interment.kind_invalid"),
    };

    public static bool TryParse(string? code, out IntermentKind kind)
    {
        (var parsed, kind) = code switch
        {
            Coffin => (true, IntermentKind.Coffin),
            Urn => (true, IntermentKind.Urn),
            _ => (false, default),
        };
        return parsed;
    }

    public static IntermentKind Parse(string? code) =>
        TryParse(code, out var kind) ? kind : throw new DomainRuleException("interment.kind_invalid");

    public static IReadOnlyList<IntermentKind> AllowedFor(GraveSiteKind siteKind) => siteKind switch
    {
        GraveSiteKind.UrnNiche => [IntermentKind.Urn],
        GraveSiteKind.Ossuary => [IntermentKind.Urn],
        GraveSiteKind.Memorial => [],
        GraveSiteKind.SingleGrave or GraveSiteKind.Family or GraveSiteKind.Tomb => [IntermentKind.Coffin, IntermentKind.Urn],
        _ => throw new DomainRuleException("grave_site.kind_invalid"),
    };

    public static void RequireAllowed(GraveSiteKind siteKind, IntermentKind kind)
    {
        if (!AllowedFor(siteKind).Contains(kind))
            throw new DomainRuleException("interment.kind_invalid");
    }
}

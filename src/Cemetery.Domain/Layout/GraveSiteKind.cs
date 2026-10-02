using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Layout;

public enum GraveSiteKind
{
    SingleGrave = 1,
    Family = 2,
    Tomb = 3,
    UrnNiche = 4,
    Ossuary = 5,
    Memorial = 6,
}

public static class GraveSiteKinds
{
    public const string SingleGraveCode = "single";
    public const string Family = "family";
    public const string Tomb = "tomb";
    public const string UrnNiche = "urn_niche";
    public const string Ossuary = "ossuary";
    public const string Memorial = "memorial";

    public static string ToCode(GraveSiteKind kind) => kind switch
    {
        GraveSiteKind.SingleGrave => SingleGraveCode,
        GraveSiteKind.Family => Family,
        GraveSiteKind.Tomb => Tomb,
        GraveSiteKind.UrnNiche => UrnNiche,
        GraveSiteKind.Ossuary => Ossuary,
        GraveSiteKind.Memorial => Memorial,
        _ => throw new DomainRuleException("grave_site.kind_invalid"),
    };

    public static bool TryParse(string? code, out GraveSiteKind kind)
    {
        (var parsed, kind) = code switch
        {
            SingleGraveCode => (true, GraveSiteKind.SingleGrave),
            Family => (true, GraveSiteKind.Family),
            Tomb => (true, GraveSiteKind.Tomb),
            UrnNiche => (true, GraveSiteKind.UrnNiche),
            Ossuary => (true, GraveSiteKind.Ossuary),
            Memorial => (true, GraveSiteKind.Memorial),
            _ => (false, default),
        };
        return parsed;
    }

    public static int DefaultCapacity(GraveSiteKind kind) => kind switch
    {
        GraveSiteKind.SingleGrave => 1,
        GraveSiteKind.Family => 2,
        GraveSiteKind.Tomb => 6,
        GraveSiteKind.UrnNiche => 1,
        GraveSiteKind.Ossuary => 1,
        GraveSiteKind.Memorial => 0,
        _ => throw new DomainRuleException("grave_site.kind_invalid"),
    };
}

using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Layout;

public enum GraveSiteStatus
{
    Planned = 1,
    Available = 2,
    Reserved = 3,
    Occupied = 4,
    Full = 5,
    Reusable = 6,
    Closed = 7,
}

public static class GraveSiteStatuses
{
    public static GraveSiteStatus Derive(bool closed, bool hasGeometry, bool reserved, int occupied, int capacity, bool reusable)
    {
        if (closed)
            return GraveSiteStatus.Closed;
        if (!hasGeometry)
            return GraveSiteStatus.Planned;
        if (occupied <= 0 && reserved)
            return GraveSiteStatus.Reserved;
        if (occupied <= 0 && reusable)
            return GraveSiteStatus.Reusable;
        if (occupied <= 0)
            return GraveSiteStatus.Available;
        if (capacity > 0 && occupied >= capacity)
            return GraveSiteStatus.Full;
        return GraveSiteStatus.Occupied;
    }

    public static string ToCode(GraveSiteStatus status) => status switch
    {
        GraveSiteStatus.Planned => "planned",
        GraveSiteStatus.Available => "available",
        GraveSiteStatus.Reserved => "reserved",
        GraveSiteStatus.Occupied => "occupied",
        GraveSiteStatus.Full => "full",
        GraveSiteStatus.Reusable => "reusable",
        GraveSiteStatus.Closed => "closed",
        _ => throw new DomainRuleException("grave_site.status_invalid"),
    };
}

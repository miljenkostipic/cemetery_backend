using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;

namespace Cemetery.Domain.Launch;

public readonly record struct OccupancyCount(int SiteCount, int OccupiedSites, int FullSites, int ClosedSites);

public readonly record struct CapacityCount(int Capacity, int FreePositions, int ReusablePositions);

public readonly record struct ReportSite(bool Closed, bool HasGeometry, int Capacity, int Occupied, bool Reusable);

public static class CemeteryReports
{
    public const int UpcomingDays = 365;

    public static OccupancyCount Occupancy(IEnumerable<ReportSite> sites)
    {
        var count = 0;
        var occupied = 0;
        var full = 0;
        var closed = 0;
        foreach (var site in sites)
            Tally(site, ref count, ref occupied, ref full, ref closed);

        return new OccupancyCount(count, occupied, full, closed);
    }

    public static CapacityCount Capacity(IEnumerable<PositionState> positions)
    {
        var capacity = 0;
        var free = 0;
        var reusable = 0;
        foreach (var state in positions)
            TallyPosition(state, ref capacity, ref free, ref reusable);

        return new CapacityCount(capacity, free, reusable);
    }

    public static DateOnly? EndWithinYear(DateOnly buriedOn, DateOnly today, int restYears)
    {
        var ends = buriedOn.AddYears(RestPeriod.Require(restYears));
        if (ends < today || ends > today.AddDays(UpcomingDays))
            return null;

        return ends;
    }

    private static void Tally(ReportSite site, ref int count, ref int occupied, ref int full, ref int closed)
    {
        count++;
        var status = GraveSiteStatuses.Derive(site.Closed, site.HasGeometry, false, site.Occupied, site.Capacity, site.Reusable);
        if (status == GraveSiteStatus.Occupied)
            occupied++;
        if (status == GraveSiteStatus.Full)
            full++;
        if (status == GraveSiteStatus.Closed)
            closed++;
    }

    private static void TallyPosition(PositionState state, ref int capacity, ref int free, ref int reusable)
    {
        capacity++;
        if (state == PositionState.Free)
            free++;
        if (state == PositionState.Reusable)
            reusable++;
    }
}

using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;

namespace Cemetery.Domain.Register;

public enum PositionState
{
    Free = 1,
    Occupied = 2,
    Reusable = 3,
}

public static class PositionStates
{
    public static string ToCode(PositionState state) => state switch
    {
        PositionState.Free => "free",
        PositionState.Occupied => "occupied",
        PositionState.Reusable => "reusable",
        _ => throw new DomainRuleException("interment.position_invalid"),
    };
}

public readonly record struct SiteUse(int Occupied, bool Reusable);

public readonly record struct OpenSlot(int Position, PositionState State);

public static class BurialPositions
{
    public static int RequireSlot(int position, int capacity)
    {
        if (position < 1 || position > capacity)
            throw new DomainRuleException("interment.position_invalid");

        return position;
    }

    public static PositionState State(int position, IEnumerable<Interment> interments, DateOnly on, int restYears)
    {
        var current = Current(position, interments);
        if (current is null)
            return PositionState.Free;
        if (RestPeriod.HasElapsed(current.BuriedOn, on, restYears))
            return PositionState.Reusable;

        return PositionState.Occupied;
    }

    public static Interment? Current(int position, IEnumerable<Interment> interments)
    {
        Interment? found = null;
        foreach (var item in interments)
        {
            if (item.Position != position || !item.Occupies)
                continue;
            if (found is null || item.BuriedOn > found.BuriedOn)
                found = item;
        }

        return found;
    }
}

public static class SiteOccupancy
{
    public static SiteUse Summarize(Guid graveSiteId, int capacity, IEnumerable<Interment> interments, DateOnly on, int restYears)
    {
        var here = ForSite(graveSiteId, interments);
        var occupied = 0;
        var reusable = false;
        for (var slot = 1; slot <= capacity; slot++)
            Tally(BurialPositions.State(slot, here, on, restYears), ref occupied, ref reusable);

        return new SiteUse(occupied, occupied == 0 && reusable);
    }

    public static IReadOnlyList<OpenSlot> OpenSlots(GraveSite site, IEnumerable<Interment> interments, DateOnly on, int restYears)
    {
        if (site.Closed || site.Capacity < 1)
            return [];

        var here = ForSite(site.Id, interments);
        var open = new List<OpenSlot>();
        for (var slot = 1; slot <= site.Capacity; slot++)
        {
            var state = BurialPositions.State(slot, here, on, restYears);
            if (state != PositionState.Occupied)
                open.Add(new OpenSlot(slot, state));
        }

        return open;
    }

    private static void Tally(PositionState state, ref int occupied, ref bool reusable)
    {
        if (state == PositionState.Occupied)
            occupied++;
        if (state == PositionState.Reusable)
            reusable = true;
    }

    private static Interment[] ForSite(Guid graveSiteId, IEnumerable<Interment> interments) =>
        interments.Where(item => item.GraveSiteId == graveSiteId).ToArray();
}

public static class BurialBook
{
    public static Interment Place(
        Deceased deceased,
        GraveSite site,
        int position,
        IntermentKind kind,
        DateOnly buriedOn,
        IReadOnlyList<Interment> atSite,
        IReadOnlyList<Interment> ofDeceased,
        int restYears)
    {
        RequireSamePlace(deceased, site);
        Prepare(site, position, kind, buriedOn, atSite, restYears);
        LifeDates.RequireBurial(deceased.DiedOn, buriedOn);
        RequireUnburied(deceased.Id, ofDeceased);
        return Interment.Record(site.TenantId, site.CemeteryId, deceased.Id, site.Id, position, kind, buriedOn);
    }

    public static Interment Transfer(
        Interment source,
        GraveSite destination,
        int position,
        DateOnly on,
        IReadOnlyList<Interment> atDestination,
        int restYears)
    {
        RequireMovable(source, destination, position, on);
        Prepare(destination, position, source.Kind, on, atDestination, restYears);
        source.MarkTransferred(on);
        return Interment.Record(destination.TenantId, destination.CemeteryId, source.DeceasedId, destination.Id, position, source.Kind, on);
    }

    private static void Prepare(GraveSite site, int position, IntermentKind kind, DateOnly on, IReadOnlyList<Interment> atSite, int restYears)
    {
        if (site.Closed)
            throw new DomainRuleException("grave_site.closed");

        IntermentKinds.RequireAllowed(site.Kind, kind);
        BurialPositions.RequireSlot(position, site.Capacity);
        Accept(position, atSite, on, restYears);
    }

    private static void Accept(int position, IReadOnlyList<Interment> atSite, DateOnly on, int restYears)
    {
        var state = BurialPositions.State(position, atSite, on, restYears);
        if (state == PositionState.Occupied)
            throw new DomainRuleException("interment.position_taken");
        if (state == PositionState.Reusable)
            BurialPositions.Current(position, atSite)?.Supersede(on);
    }

    private static void RequireSamePlace(Deceased deceased, GraveSite site)
    {
        if (deceased.CemeteryId != site.CemeteryId || deceased.TenantId != site.TenantId)
            throw new DomainRuleException("interment.location_invalid");
    }

    private static void RequireUnburied(Guid deceasedId, IEnumerable<Interment> history)
    {
        foreach (var item in history)
        {
            if (item.DeceasedId == deceasedId && item.Occupies)
                throw new DomainRuleException("interment.already_buried");
        }
    }

    private static void RequireMovable(Interment source, GraveSite destination, int position, DateOnly on)
    {
        if (!source.Occupies || source.CemeteryId != destination.CemeteryId)
            throw new DomainRuleException("interment.transfer_invalid");
        if (source.GraveSiteId == destination.Id && source.Position == position)
            throw new DomainRuleException("interment.transfer_invalid");
        if (on < source.BuriedOn)
            throw new DomainRuleException("interment.date_invalid");
    }
}

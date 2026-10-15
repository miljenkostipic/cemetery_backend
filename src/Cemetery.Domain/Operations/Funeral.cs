using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Operations;

public enum FuneralPlace
{
    Chapel = 1,
    Mortuary = 2,
    Graveside = 3,
}

public enum FuneralStatus
{
    Scheduled = 1,
    Cancelled = 2,
}

public static class FuneralPlaces
{
    public static string ToCode(FuneralPlace place) => place switch
    {
        FuneralPlace.Chapel => "chapel",
        FuneralPlace.Mortuary => "mortuary",
        FuneralPlace.Graveside => "graveside",
        _ => throw new DomainRuleException("funeral.place_invalid"),
    };

    public static FuneralPlace Parse(string? code) => code switch
    {
        "chapel" => FuneralPlace.Chapel,
        "mortuary" => FuneralPlace.Mortuary,
        "graveside" => FuneralPlace.Graveside,
        _ => throw new DomainRuleException("funeral.place_invalid"),
    };
}

public static class FuneralStatuses
{
    public static string ToCode(FuneralStatus status) => status switch
    {
        FuneralStatus.Scheduled => "scheduled",
        FuneralStatus.Cancelled => "cancelled",
        _ => throw new DomainRuleException("funeral.date_invalid"),
    };

    public static FuneralStatus Parse(string? code) => code switch
    {
        "scheduled" => FuneralStatus.Scheduled,
        "cancelled" => FuneralStatus.Cancelled,
        _ => throw new DomainRuleException("funeral.date_invalid"),
    };
}

public sealed class Funeral : TenantEntity
{
    private Funeral()
    {
    }

    public Guid CemeteryId { get; private set; }

    public Guid DeceasedId { get; private set; }

    public Guid GraveSiteId { get; private set; }

    public DateTime StartsAt { get; private set; }

    public DateTime EndsAt { get; private set; }

    public FuneralPlace Place { get; private set; }

    public FuneralStatus Status { get; private set; }

    public static Funeral Schedule(
        Guid tenantId,
        Guid cemeteryId,
        Guid deceasedId,
        Guid graveSiteId,
        string? startsAt,
        string? endsAt,
        string? place)
    {
        RequireParties(tenantId, cemeteryId, deceasedId, graveSiteId);
        var start = CivilTime.Parse(startsAt, "funeral.date_invalid");
        var end = CivilTime.Parse(endsAt, "funeral.date_invalid");
        ScheduleWindow.RequireOrdered(start, end, "funeral.date_invalid");
        return Create(tenantId, cemeteryId, deceasedId, graveSiteId, start, end, FuneralPlaces.Parse(place));
    }

    public void Cancel()
    {
        if (Status == FuneralStatus.Cancelled)
            return;
        Status = FuneralStatus.Cancelled;
    }

    public bool Clashes(Funeral other) =>
        Blocks(other.Status, other.StartsAt, other.EndsAt, other.GraveSiteId, other.Place);

    public bool Clashes(ChapelBooking booking)
    {
        if (Status == FuneralStatus.Cancelled || Place == FuneralPlace.Graveside)
            return false;
        if (FuneralPlaces.ToCode(Place) != ChapelPlaces.ToCode(booking.Place))
            return false;
        return ScheduleWindow.Overlap(StartsAt, EndsAt, booking.StartsAt, booking.EndsAt);
    }

    private static Funeral Create(
        Guid tenantId,
        Guid cemeteryId,
        Guid deceasedId,
        Guid graveSiteId,
        DateTime start,
        DateTime end,
        FuneralPlace place) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = cemeteryId,
            DeceasedId = deceasedId,
            GraveSiteId = graveSiteId,
            StartsAt = start,
            EndsAt = end,
            Place = place,
            Status = FuneralStatus.Scheduled,
        };

    private static void RequireParties(Guid tenantId, Guid cemeteryId, Guid deceasedId, Guid graveSiteId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");
        if (cemeteryId == Guid.Empty)
            throw new DomainRuleException("cemetery.not_found");
        if (deceasedId == Guid.Empty)
            throw new DomainRuleException("funeral.deceased_invalid");
        if (graveSiteId == Guid.Empty)
            throw new DomainRuleException("funeral.site_invalid");
    }

    private bool Blocks(FuneralStatus status, DateTime start, DateTime end, Guid graveSiteId, FuneralPlace place)
    {
        if (Status == FuneralStatus.Cancelled || status == FuneralStatus.Cancelled)
            return false;
        if (!ScheduleWindow.Overlap(StartsAt, EndsAt, start, end))
            return false;
        if (GraveSiteId == graveSiteId)
            return true;
        return Place != FuneralPlace.Graveside && Place == place;
    }
}

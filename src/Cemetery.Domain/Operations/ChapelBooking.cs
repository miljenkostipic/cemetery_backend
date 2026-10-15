using Cemetery.Domain.Identity;

namespace Cemetery.Domain.Operations;

public enum ChapelPlace
{
    Chapel = 1,
    Mortuary = 2,
}

public static class ChapelPlaces
{
    public static string ToCode(ChapelPlace place) => place switch
    {
        ChapelPlace.Chapel => "chapel",
        ChapelPlace.Mortuary => "mortuary",
        _ => throw new DomainRuleException("chapel.place_invalid"),
    };

    public static ChapelPlace Parse(string? code) => code switch
    {
        "chapel" => ChapelPlace.Chapel,
        "mortuary" => ChapelPlace.Mortuary,
        _ => throw new DomainRuleException("chapel.place_invalid"),
    };
}

public sealed class ChapelBooking : TenantEntity
{
    private ChapelBooking()
    {
    }

    public Guid CemeteryId { get; private set; }

    public ChapelPlace Place { get; private set; }

    public DateTime StartsAt { get; private set; }

    public DateTime EndsAt { get; private set; }

    public static ChapelBooking Book(Guid tenantId, Guid cemeteryId, string? place, string? startsAt, string? endsAt)
    {
        RequireParties(tenantId, cemeteryId);
        var start = CivilTime.Parse(startsAt, "chapel.date_invalid");
        var end = CivilTime.Parse(endsAt, "chapel.date_invalid");
        ScheduleWindow.RequireOrdered(start, end, "chapel.date_invalid");
        return Create(tenantId, cemeteryId, ChapelPlaces.Parse(place), start, end);
    }

    public bool Clashes(ChapelBooking other) =>
        Place == other.Place && ScheduleWindow.Overlap(StartsAt, EndsAt, other.StartsAt, other.EndsAt);

    public bool Clashes(Funeral funeral) => funeral.Clashes(this);

    private static ChapelBooking Create(Guid tenantId, Guid cemeteryId, ChapelPlace place, DateTime start, DateTime end) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CemeteryId = cemeteryId,
            Place = place,
            StartsAt = start,
            EndsAt = end,
        };

    private static void RequireParties(Guid tenantId, Guid cemeteryId)
    {
        if (tenantId == Guid.Empty)
            throw new DomainRuleException("tenant.required");
        if (cemeteryId == Guid.Empty)
            throw new DomainRuleException("cemetery.not_found");
    }
}

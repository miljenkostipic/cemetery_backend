using Cemetery.Application.Layout;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Operations;
using Cemetery.Domain.Register;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Application.Operations;

public sealed record FuneralView(
    Guid Id,
    Guid CemeteryId,
    Guid DeceasedId,
    string DeceasedName,
    Guid GraveSiteId,
    string GraveSiteCode,
    string StartsAt,
    string EndsAt,
    string Place,
    string Status);

public sealed record ChapelBookingView(Guid Id, Guid CemeteryId, string Place, string StartsAt, string EndsAt);

public sealed record WorkOrderView(
    Guid Id,
    Guid CemeteryId,
    Guid GraveSiteId,
    string GraveSiteCode,
    string Kind,
    string Status,
    DateOnly ScheduledOn,
    string Notes,
    Guid? PermitId);

public sealed record MonumentPermitView(
    Guid Id,
    Guid CemeteryId,
    Guid GraveSiteId,
    string GraveSiteCode,
    string HolderName,
    DateOnly IssuedOn,
    DateOnly ExpiresOn,
    string Status);

public sealed record FieldTaskView(
    Guid WorkOrderId,
    Guid CemeteryId,
    string CemeteryName,
    Guid GraveSiteId,
    string GraveSiteCode,
    string Kind,
    string Status,
    DateOnly ScheduledOn,
    string Notes,
    GeoPointView? Location);

public sealed record CompletionView(Guid WorkOrderId, string Status, DateOnly CompletedOn);

public sealed record PhotoView(Guid Id, Guid WorkOrderId, string ContentType);

internal static class OperationsMaps
{
    public static FuneralView Funeral(Funeral funeral, Deceased? person, GraveSite? site) =>
        new(
            funeral.Id,
            funeral.CemeteryId,
            funeral.DeceasedId,
            NameOf(person),
            funeral.GraveSiteId,
            CodeOf(site),
            CivilTime.Format(funeral.StartsAt),
            CivilTime.Format(funeral.EndsAt),
            FuneralPlaces.ToCode(funeral.Place),
            FuneralStatuses.ToCode(funeral.Status));

    public static ChapelBookingView Booking(ChapelBooking booking) =>
        new(booking.Id, booking.CemeteryId, ChapelPlaces.ToCode(booking.Place), CivilTime.Format(booking.StartsAt), CivilTime.Format(booking.EndsAt));

    public static WorkOrderView Order(WorkOrder order, GraveSite? site) =>
        new(
            order.Id,
            order.CemeteryId,
            order.GraveSiteId,
            CodeOf(site),
            WorkKinds.ToCode(order.Kind),
            WorkOrderStatuses.ToCode(order.Status),
            order.ScheduledOn,
            order.Notes,
            order.PermitId);

    public static MonumentPermitView Permit(MonumentPermit permit, GraveSite? site, DateOnly today) =>
        new(
            permit.Id,
            permit.CemeteryId,
            permit.GraveSiteId,
            CodeOf(site),
            permit.HolderName,
            permit.IssuedOn,
            permit.ExpiresOn,
            permit.StatusOn(today));

    public static FieldTaskView Task(WorkOrder order, CemeteryPlace? cemetery, GraveSite? site) =>
        new(
            order.Id,
            order.CemeteryId,
            cemetery?.Name ?? "",
            order.GraveSiteId,
            CodeOf(site),
            WorkKinds.ToCode(order.Kind),
            WorkOrderStatuses.ToCode(order.Status),
            order.ScheduledOn,
            order.Notes,
            Location(site));

    public static GeoPointView? Location(GraveSite? site)
    {
        if (site?.Outline is not GeoPolygon outline)
            return null;
        return Average(outline.Ring);
    }

    public static string NameOf(Deceased? person) =>
        person is null ? "" : string.Join(' ', person.GivenName, person.FamilyName).Trim();

    public static string CodeOf(GraveSite? site) => site?.Code ?? "";

    private static GeoPointView Average(IReadOnlyList<GeoPoint> ring)
    {
        var count = ring.Count - 1;
        double longitude = 0;
        double latitude = 0;
        for (var index = 0; index < count; index++)
        {
            longitude += ring[index].Longitude;
            latitude += ring[index].Latitude;
        }

        return new GeoPointView(longitude / count, latitude / count);
    }
}

internal static class OperationsIndex
{
    public static Dictionary<Guid, T> ById<T>(IEnumerable<T> items, Func<T, Guid> id)
    {
        var map = new Dictionary<Guid, T>();
        foreach (var item in items)
            map[id(item)] = item;
        return map;
    }
}

internal static class OperationsClock
{
    public static DateOnly Today(TimeProvider clock) =>
        DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}

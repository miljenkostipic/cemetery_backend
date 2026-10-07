using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;
using Cemetery.Application.Layout;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Application.Register;

public sealed record DeceasedView(
    Guid Id,
    Guid CemeteryId,
    string GivenName,
    string FamilyName,
    DateOnly? BornOn,
    DateOnly? DiedOn,
    int Version,
    Guid? IntermentId,
    Guid? GraveSiteId,
    string? GraveSiteCode,
    int? Position,
    string? Epitaph,
    string? PhotoUrl);

public sealed record IntermentView(
    Guid Id,
    Guid DeceasedId,
    string GivenName,
    string FamilyName,
    Guid GraveSiteId,
    string GraveSiteCode,
    int Position,
    string Kind,
    DateOnly BuriedOn);

public sealed record FreePositionView(
    Guid GraveSiteId,
    string GraveSiteCode,
    int Position,
    string State,
    IReadOnlyList<string> Kinds);

public sealed record DeceasedBody(string GivenName, string FamilyName, DateOnly? BornOn, DateOnly? DiedOn);

public sealed record CorrectionBody(string GivenName, string FamilyName, DateOnly? BornOn, DateOnly? DiedOn, string Reason);

public sealed record IntermentBody(Guid DeceasedId, Guid GraveSiteId, int Position, string Kind, DateOnly BuriedOn);

public sealed record ExhumationBody(DateOnly On);

public sealed record TransferBody(Guid GraveSiteId, int Position, DateOnly On);

public sealed record RestPeriodBody(int Years);

internal static class RestYears
{
    public static int Of(CemeteryPlace? cemetery) =>
        cemetery is { RestPeriodYears: >= RestPeriod.MinimumYears } found ? found.RestPeriodYears : RestPeriod.DefaultYears;
}

internal static class RegisterMaps
{
    public static DeceasedView ToView(Deceased person, Interment? interment, string? graveSiteCode) =>
        new(
            person.Id,
            person.CemeteryId,
            person.GivenName,
            person.FamilyName,
            person.BornOn,
            person.DiedOn,
            person.Version,
            interment?.Id,
            interment?.GraveSiteId,
            graveSiteCode,
            interment?.Position,
            person.Epitaph,
            person.PhotoUrl);

    public static IntermentView ToView(Interment interment, Deceased person, string graveSiteCode) =>
        new(
            interment.Id,
            interment.DeceasedId,
            person.GivenName,
            person.FamilyName,
            interment.GraveSiteId,
            graveSiteCode,
            interment.Position,
            IntermentKinds.ToCode(interment.Kind),
            interment.BuriedOn);

    public static Interment? Occupying(Guid deceasedId, IEnumerable<Interment> interments)
    {
        foreach (var item in interments)
        {
            if (item.DeceasedId == deceasedId && item.Occupies)
                return item;
        }

        return null;
    }

    public static string? CodeOf(Guid? graveSiteId, IEnumerable<GraveSite> sites)
    {
        if (graveSiteId is not Guid id)
            return null;

        foreach (var site in sites)
        {
            if (site.Id == id)
                return site.Code;
        }

        return null;
    }

    public static GraveSiteView[] SiteViews(IReadOnlyList<GraveSite> sites, IReadOnlyList<Interment> interments, int restYears, DateOnly today)
    {
        var views = new GraveSiteView[sites.Count];
        for (var index = 0; index < sites.Count; index++)
        {
            var site = sites[index];
            var use = SiteOccupancy.Summarize(site.Id, site.Capacity, interments, today, restYears);
            views[index] = LayoutMaps.ToView(site, use);
        }

        return views;
    }

    public static FreePositionView[] Free(IReadOnlyList<GraveSite> sites, IReadOnlyList<Interment> interments, Guid? graveSiteId, int restYears, DateOnly today)
    {
        var found = new List<FreePositionView>();
        foreach (var site in sites)
        {
            if (graveSiteId is Guid only && site.Id != only)
                continue;
            Append(found, site, interments, restYears, today);
        }

        return found.ToArray();
    }

    private static void Append(List<FreePositionView> found, GraveSite site, IReadOnlyList<Interment> interments, int restYears, DateOnly today)
    {
        var kinds = IntermentKinds.AllowedFor(site.Kind).Select(IntermentKinds.ToCode).ToArray();
        foreach (var slot in SiteOccupancy.OpenSlots(site, interments, today, restYears))
            found.Add(new FreePositionView(site.Id, site.Code, slot.Position, PositionStates.ToCode(slot.State), kinds));
    }
}

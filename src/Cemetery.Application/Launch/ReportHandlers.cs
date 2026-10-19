using System.Security.Cryptography;
using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Register;
using Cemetery.Domain.Launch;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;
using FluentValidation;

namespace Cemetery.Application.Launch;

public sealed record OccupancyView(int SiteCount, int OccupiedSites, int FullSites, int ClosedSites);

public sealed record FreeCapacityView(int Capacity, int FreePositions, int ReusablePositions);

public sealed record RestPeriodEndView(Guid GraveSiteId, string GraveSiteCode, int Position, DateOnly EndsOn);

public sealed record RevenueView(string Currency, string Charged, string Paid, string Outstanding);

public sealed record CemeteryReportView(
    OccupancyView Occupancy,
    FreeCapacityView FreeCapacity,
    IReadOnlyList<RestPeriodEndView> RestPeriodEnds,
    IReadOnlyList<RevenueView> Revenue);

public sealed record GetCemeteryReport(Guid CemeteryId);

public sealed class GetCemeteryReportHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IRegisterRepository register,
    TimeProvider clock) : IQueryHandler<GetCemeteryReport, CemeteryReportView>
{
    public async Task<CemeteryReportView> Handle(GetCemeteryReport query, CancellationToken cancellationToken)
    {
        await RegisterAccess.RequireClerkAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var cemetery = await layouts.FindCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("report.not_found");
        var sites = await layouts.ListByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        var interments = await register.ListIntermentsByCemeteryAsync(query.CemeteryId, cancellationToken).ConfigureAwait(false);
        return ReportMaps.Compose(sites, interments, RestYears.Of(cemetery), RegisterClock.Today(clock));
    }
}

internal static class ReportMaps
{
    public static CemeteryReportView Compose(
        IReadOnlyList<GraveSite> sites,
        IReadOnlyList<Interment> interments,
        int restYears,
        DateOnly today)
    {
        var occupancy = CemeteryReports.Occupancy(Snapshots(sites, interments, restYears, today));
        var capacity = CemeteryReports.Capacity(OpenPositions(sites, interments, restYears, today));
        return new CemeteryReportView(Occupancy(occupancy), Capacity(capacity), Ends(sites, interments, restYears, today), []);
    }

    private static OccupancyView Occupancy(OccupancyCount count) =>
        new(count.SiteCount, count.OccupiedSites, count.FullSites, count.ClosedSites);

    private static FreeCapacityView Capacity(CapacityCount count) =>
        new(count.Capacity, count.FreePositions, count.ReusablePositions);

    private static ReportSite[] Snapshots(IReadOnlyList<GraveSite> sites, IReadOnlyList<Interment> interments, int restYears, DateOnly today)
    {
        var snapshots = new ReportSite[sites.Count];
        for (var index = 0; index < sites.Count; index++)
            snapshots[index] = Snapshot(sites[index], interments, restYears, today);

        return snapshots;
    }

    private static ReportSite Snapshot(GraveSite site, IReadOnlyList<Interment> interments, int restYears, DateOnly today)
    {
        var use = SiteOccupancy.Summarize(site.Id, site.Capacity, interments, today, restYears);
        return new ReportSite(site.Closed, site.Outline is not null, site.Capacity, use.Occupied, use.Reusable);
    }

    private static List<PositionState> OpenPositions(IReadOnlyList<GraveSite> sites, IReadOnlyList<Interment> interments, int restYears, DateOnly today)
    {
        var states = new List<PositionState>();
        foreach (var site in sites)
            AddOpen(states, site, interments, restYears, today);

        return states;
    }

    private static void AddOpen(List<PositionState> states, GraveSite site, IReadOnlyList<Interment> interments, int restYears, DateOnly today)
    {
        if (site.Closed)
            return;

        var here = interments.Where(item => item.GraveSiteId == site.Id).ToArray();
        for (var slot = 1; slot <= site.Capacity; slot++)
            states.Add(BurialPositions.State(slot, here, today, restYears));
    }

    private static List<RestPeriodEndView> Ends(IReadOnlyList<GraveSite> sites, IReadOnlyList<Interment> interments, int restYears, DateOnly today)
    {
        var ends = new List<RestPeriodEndView>();
        foreach (var interment in interments)
            AddEnd(ends, sites, interment, restYears, today);

        ends.Sort(static (left, right) => left.EndsOn.CompareTo(right.EndsOn));
        return ends;
    }

    private static void AddEnd(List<RestPeriodEndView> ends, IReadOnlyList<GraveSite> sites, Interment interment, int restYears, DateOnly today)
    {
        if (!interment.Occupies)
            return;

        var endsOn = CemeteryReports.EndWithinYear(interment.BuriedOn, today, restYears);
        if (endsOn is not DateOnly on)
            return;

        ends.Add(new RestPeriodEndView(interment.GraveSiteId, RegisterMaps.CodeOf(interment.GraveSiteId, sites) ?? "", interment.Position, on));
    }
}

internal static class WebhookSecrets
{
    public static string Create()
    {
        var encoded = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

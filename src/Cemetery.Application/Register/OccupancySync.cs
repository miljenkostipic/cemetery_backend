using Cemetery.Application.Abstractions;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;
using Cemetery.Domain.Rights;

namespace Cemetery.Application.Register;

public static class OccupancySync
{
    public static async Task Refresh(
        GraveSite site,
        ILayoutRepository layouts,
        IRegisterRepository register,
        IRightsRepository rights,
        TimeProvider clock,
        CancellationToken cancellationToken) =>
        await Refresh(site, layouts, register, rights, clock, null, null, cancellationToken).ConfigureAwait(false);

    public static async Task Refresh(
        GraveSite site,
        ILayoutRepository layouts,
        IRegisterRepository register,
        IRightsRepository rights,
        TimeProvider clock,
        bool? reserved,
        IEnumerable<Interment>? extra,
        CancellationToken cancellationToken)
    {
        var cemetery = await layouts.FindCemeteryAsync(site.CemeteryId, cancellationToken).ConfigureAwait(false);
        var interments = await register.ListIntermentsBySiteAsync(site.Id, cancellationToken).ConfigureAwait(false);
        var active = reserved ?? await rights.FindActiveReservationAsync(site.Id, cancellationToken).ConfigureAwait(false) is not null;
        var rest = cemetery?.RestPeriodYears ?? RestPeriod.DefaultYears;
        site.Apply(SiteOccupancy.Describe(site, active, With(interments, extra), RegisterClock.Today(clock), rest));
    }

    private static IEnumerable<Interment> With(IReadOnlyList<Interment> stored, IEnumerable<Interment>? extra) =>
        extra is null ? stored : stored.Concat(extra);
}

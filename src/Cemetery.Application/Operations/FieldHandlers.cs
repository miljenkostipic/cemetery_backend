using Cemetery.Application.Abstractions;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Operations;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Application.Operations;

public sealed record ListFieldTasks;

public sealed class ListFieldTasksHandler(
    ICurrentUser current,
    ITenantContext tenant,
    IMembershipRepository memberships,
    ILayoutRepository layouts,
    IOperationsRepository store,
    TimeProvider clock) : IQueryHandler<ListFieldTasks, IReadOnlyList<FieldTaskView>>
{
    public async Task<IReadOnlyList<FieldTaskView>> Handle(ListFieldTasks query, CancellationToken cancellationToken)
    {
        _ = await OperationsAccess.RequireFieldAsync(current, tenant, memberships, cancellationToken).ConfigureAwait(false);
        var orders = await store.ListOpenOnAsync(OperationsClock.Today(clock), cancellationToken).ConfigureAwait(false);
        var cemeteries = OperationsIndex.ById(await layouts.ListCemeteriesAsync(cancellationToken).ConfigureAwait(false), cemetery => cemetery.Id);
        var sites = await Sites(cemeteries.Values, cancellationToken).ConfigureAwait(false);
        return orders.Select(order => Map(order, cemeteries, sites)).ToArray();
    }

    private async Task<Dictionary<Guid, GraveSite>> Sites(IEnumerable<CemeteryPlace> cemeteries, CancellationToken cancellationToken)
    {
        var sites = new List<GraveSite>();
        foreach (var cemetery in cemeteries)
            sites.AddRange(await layouts.ListByCemeteryAsync(cemetery.Id, cancellationToken).ConfigureAwait(false));
        return OperationsIndex.ById(sites, site => site.Id);
    }

    private static FieldTaskView Map(
        WorkOrder order,
        Dictionary<Guid, CemeteryPlace> cemeteries,
        Dictionary<Guid, GraveSite> sites)
    {
        cemeteries.TryGetValue(order.CemeteryId, out var cemetery);
        sites.TryGetValue(order.GraveSiteId, out var site);
        return OperationsMaps.Task(order, cemetery, site);
    }
}

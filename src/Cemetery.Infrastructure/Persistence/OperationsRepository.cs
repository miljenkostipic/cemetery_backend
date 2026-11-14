using System.Globalization;
using Cemetery.Application.Abstractions;
using Cemetery.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Infrastructure.Persistence;

internal sealed class OperationsRepository(CemeteryDbContext db) : IOperationsRepository
{
    public Task AddFuneralAsync(Funeral funeral, CancellationToken cancellationToken)
    {
        db.Funerals.Add(funeral);
        return Task.CompletedTask;
    }

    public Task<Funeral?> FindFuneralAsync(Guid id, CancellationToken cancellationToken) =>
        db.Funerals.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public void RemoveFuneral(Funeral funeral) => db.Funerals.Remove(funeral);

    public async Task<IReadOnlyList<Funeral>> ListFuneralsAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.Funerals.Where(entity => entity.CemeteryId == cemeteryId)
            .OrderBy(entity => entity.StartsAt).ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task AddBookingAsync(ChapelBooking booking, CancellationToken cancellationToken)
    {
        db.ChapelBookings.Add(booking);
        return Task.CompletedTask;
    }

    public Task<ChapelBooking?> FindBookingAsync(Guid id, CancellationToken cancellationToken) =>
        db.ChapelBookings.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public void RemoveBooking(ChapelBooking booking) => db.ChapelBookings.Remove(booking);

    public async Task<IReadOnlyList<ChapelBooking>> ListBookingsAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.ChapelBookings.Where(entity => entity.CemeteryId == cemeteryId)
            .OrderBy(entity => entity.StartsAt).ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task AddWorkOrderAsync(WorkOrder order, CancellationToken cancellationToken)
    {
        db.WorkOrders.Add(order);
        return Task.CompletedTask;
    }

    public Task<WorkOrder?> FindWorkOrderAsync(Guid id, CancellationToken cancellationToken) =>
        db.WorkOrders.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WorkOrder>> ListWorkOrdersAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.WorkOrders.Where(entity => entity.CemeteryId == cemeteryId)
            .OrderBy(entity => entity.ScheduledOn).ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<WorkOrder>> ListOpenOnAsync(DateOnly day, CancellationToken cancellationToken) =>
        await db.WorkOrders.Where(entity => entity.ScheduledOn == day && entity.Status == WorkOrderStatus.Open)
            .OrderBy(entity => entity.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task RemoveWorkOrderAsync(WorkOrder order, CancellationToken cancellationToken)
    {
        var photos = await db.WorkOrderPhotos.Where(photo => photo.WorkOrderId == order.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        db.WorkOrderPhotos.RemoveRange(photos);
        db.WorkOrders.Remove(order);
    }

    public Task AddPermitAsync(MonumentPermit permit, CancellationToken cancellationToken)
    {
        db.MonumentPermits.Add(permit);
        return Task.CompletedTask;
    }

    public Task<MonumentPermit?> FindPermitAsync(Guid id, CancellationToken cancellationToken) =>
        db.MonumentPermits.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task RemovePermitAsync(MonumentPermit permit, CancellationToken cancellationToken)
    {
        var orders = await db.WorkOrders.Where(order => order.PermitId == permit.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var order in orders)
            order.ClearPermit();
        db.MonumentPermits.Remove(permit);
    }

    public async Task<IReadOnlyList<MonumentPermit>> ListPermitsAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.MonumentPermits.Where(entity => entity.CemeteryId == cemeteryId)
            .OrderBy(entity => entity.IssuedOn).ThenBy(entity => entity.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task AddPhotoAsync(WorkOrderPhoto photo, CancellationToken cancellationToken)
    {
        db.WorkOrderPhotos.Add(photo);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<WorkOrderPhoto>> ListPhotosAsync(Guid workOrderId, CancellationToken cancellationToken) =>
        await db.WorkOrderPhotos.Where(photo => photo.WorkOrderId == workOrderId)
            .OrderBy(photo => photo.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<Page<WorkOrder>> PageWorkOrdersAsync(Guid cemeteryId, WorkOrderFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var size = Pages.Size(page.Limit);
        var query = db.WorkOrders.Where(order => order.CemeteryId == cemeteryId);
        if (!string.IsNullOrWhiteSpace(filter.Query))
            query = query.Where(order => order.Notes.Contains(filter.Query));
        if (filter.Status is "open" or "completed")
        {
            var status = WorkOrderStatuses.Parse(filter.Status);
            query = query.Where(order => order.Status == status);
        }

        var cursor = KeysetCursor.Decode(page.Cursor);
        if (cursor is { } key && DateOnly.TryParse(key.Sort, out var on))
            query = query.Where(order => order.ScheduledOn > on || (order.ScheduledOn == on && order.Id.CompareTo(key.Id) > 0));
        var rows = await query.OrderBy(order => order.ScheduledOn).ThenBy(order => order.Id).Take(size + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        return KeysetPage.Pack(rows, size, order => order.ScheduledOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), order => order.Id);
    }
}

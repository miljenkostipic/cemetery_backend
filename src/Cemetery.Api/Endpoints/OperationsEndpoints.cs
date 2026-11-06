using Cemetery.Api.Tenancy;
using Cemetery.Application.Abstractions;
using Cemetery.Application.Operations;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cemetery.Api.Endpoints;

public static class OperationsEndpoints
{
    public static RouteGroupBuilder MapOperationsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/cemeteries/{cemeteryId}/funerals", GetFunerals).RequireAuthorization(Policies.Office);
        api.MapPost("/cemeteries/{cemeteryId}/funerals", PostFuneral).RequireAuthorization(Policies.Office);
        api.MapPost("/funerals/{funeralId}/cancellations", PostCancellation).RequireAuthorization(Policies.Office);
        api.MapDelete("/funerals/{funeralId}", DeleteFuneral).RequireAuthorization(Policies.Office);
        api.MapDelete("/chapel-bookings/{bookingId}", DeleteBooking).RequireAuthorization(Policies.Office);
        api.MapDelete("/work-orders/{workOrderId}", DeleteOrder).RequireAuthorization(Policies.Office);
        api.MapDelete("/monument-permits/{permitId}", DeletePermit).RequireAuthorization(Policies.Office);
        api.MapGet("/cemeteries/{cemeteryId}/chapel-bookings", GetBookings).RequireAuthorization(Policies.Office);
        api.MapPost("/cemeteries/{cemeteryId}/chapel-bookings", PostBooking).RequireAuthorization(Policies.Office);
        api.MapGet("/cemeteries/{cemeteryId}/work-orders", GetOrders).RequireAuthorization(Policies.Office);
        api.MapPost("/cemeteries/{cemeteryId}/work-orders", PostOrder).RequireAuthorization(Policies.Office);
        api.MapGet("/cemeteries/{cemeteryId}/monument-permits", GetPermits).RequireAuthorization(Policies.Office);
        api.MapPost("/cemeteries/{cemeteryId}/monument-permits", PostPermit).RequireAuthorization(Policies.Office);
        api.MapGet("/field/today", FieldToday).RequireAuthorization(Policies.Field);
        api.MapPost("/work-orders/{workOrderId}/completions", CompleteOrder).RequireAuthorization(Policies.Field);
        api.MapPost("/work-orders/{workOrderId}/photos", AddPhoto).RequireAuthorization(Policies.Field);
        return api;
    }

    private static async Task<Ok<IReadOnlyList<FuneralView>>> GetFunerals(
        Guid cemeteryId,
        IQueryHandler<ListFunerals, IReadOnlyList<FuneralView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListFunerals(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<FuneralView>> PostFuneral(
        Guid cemeteryId,
        FuneralBody body,
        ICommandHandler<ScheduleFuneral, FuneralView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(
            new ScheduleFuneral(cemeteryId, body.DeceasedId, body.GraveSiteId, body.StartsAt, body.EndsAt, body.Place),
            cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<FuneralView>> PostCancellation(
        Guid funeralId,
        ICommandHandler<CancelFuneral, FuneralView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new CancelFuneral(funeralId), cancellationToken).ConfigureAwait(false));

    private static async Task<NoContent> DeleteFuneral(
        Guid funeralId,
        ICommandHandler<RemoveFuneral, bool> handler,
        CancellationToken cancellationToken)
    {
        _ = await handler.Handle(new RemoveFuneral(funeralId), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeleteBooking(
        Guid bookingId,
        ICommandHandler<RemoveChapelBooking, bool> handler,
        CancellationToken cancellationToken)
    {
        _ = await handler.Handle(new RemoveChapelBooking(bookingId), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeleteOrder(
        Guid workOrderId,
        ICommandHandler<RemoveWorkOrder, bool> handler,
        CancellationToken cancellationToken)
    {
        _ = await handler.Handle(new RemoveWorkOrder(workOrderId), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> DeletePermit(
        Guid permitId,
        ICommandHandler<RemoveMonumentPermit, bool> handler,
        CancellationToken cancellationToken)
    {
        _ = await handler.Handle(new RemoveMonumentPermit(permitId), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<ChapelBookingView>>> GetBookings(
        Guid cemeteryId,
        IQueryHandler<ListChapelBookings, IReadOnlyList<ChapelBookingView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListChapelBookings(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<ChapelBookingView>> PostBooking(
        Guid cemeteryId,
        ChapelBookingBody body,
        ICommandHandler<BookChapel, ChapelBookingView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(
            new BookChapel(cemeteryId, body.Place, body.StartsAt, body.EndsAt),
            cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<Page<WorkOrderView>>> GetOrders(
        Guid cemeteryId,
        string? q,
        string? status,
        string? cursor,
        int? limit,
        IQueryHandler<ListWorkOrders, Page<WorkOrderView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListWorkOrders(cemeteryId, q, status, cursor, limit), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<WorkOrderView>> PostOrder(
        Guid cemeteryId,
        WorkOrderBody body,
        ICommandHandler<OpenWorkOrder, WorkOrderView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(
            new OpenWorkOrder(cemeteryId, body.GraveSiteId, body.Kind, body.ScheduledOn, body.Notes),
            cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<MonumentPermitView>>> GetPermits(
        Guid cemeteryId,
        IQueryHandler<ListPermits, IReadOnlyList<MonumentPermitView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListPermits(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<MonumentPermitView>> PostPermit(
        Guid cemeteryId,
        MonumentPermitBody body,
        ICommandHandler<IssuePermit, MonumentPermitView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(
            new IssuePermit(cemeteryId, body.GraveSiteId, body.HolderName, body.IssuedOn, body.ExpiresOn),
            cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<FieldTaskView>>> FieldToday(
        IQueryHandler<ListFieldTasks, IReadOnlyList<FieldTaskView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListFieldTasks(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<CompletionView>> CompleteOrder(
        Guid workOrderId,
        CompletionBody body,
        ICommandHandler<CompleteWorkOrder, CompletionView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(
            new CompleteWorkOrder(workOrderId, body.CompletedOn, body.Note),
            cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<PhotoView>> AddPhoto(
        Guid workOrderId,
        PhotoBody body,
        ICommandHandler<AttachWorkOrderPhoto, PhotoView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(
            new AttachWorkOrderPhoto(workOrderId, body.ContentType, body.ContentBase64),
            cancellationToken).ConfigureAwait(false));
}

public sealed record FuneralBody(Guid DeceasedId, Guid GraveSiteId, string StartsAt, string EndsAt, string Place);

public sealed record ChapelBookingBody(string Place, string StartsAt, string EndsAt);

public sealed record WorkOrderBody(Guid GraveSiteId, string Kind, DateOnly ScheduledOn, string Notes);

public sealed record MonumentPermitBody(Guid GraveSiteId, string HolderName, DateOnly IssuedOn, DateOnly ExpiresOn);

public sealed record CompletionBody(DateOnly CompletedOn, string Note);

public sealed record PhotoBody(string ContentType, string ContentBase64);

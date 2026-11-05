using Cemetery.Api.Tenancy;
using Cemetery.Application.Abstractions;
using Cemetery.Application.Billing;
using Cemetery.Application.Layout;
using Cemetery.Application.Organizations;
using Cemetery.Application.Rights;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cemetery.Api.Endpoints;

public static class Phase4Endpoints
{
    public static RouteGroupBuilder MapPhase4Endpoints(this RouteGroupBuilder api)
    {
        MapHolders(api);
        MapRights(api);
        MapPortal(api);
        MapRead(api);
        return api;
    }

    private static void MapHolders(RouteGroupBuilder api)
    {
        api.MapGet("/holders", ListHolders).RequireAuthorization(Policies.Office);
        api.MapPost("/holders", CreateHolder).RequireAuthorization(Policies.Office);
        api.MapGet("/holders/{holderId}", GetHolder).RequireAuthorization(Policies.Office);
        api.MapPut("/holders/{holderId}", UpdateHolder).RequireAuthorization(Policies.Office);
        api.MapPost("/holders/{holderId}/invitations", InviteHolder).RequireAuthorization(Policies.Office);
        api.MapGet("/me/holder", MyHolder).RequireAuthorization(Policies.Holder);
    }

    private static void MapRights(RouteGroupBuilder api)
    {
        api.MapGet("/grave-sites/{graveSiteId}/right", GetRight).RequireAuthorization(Policies.Office);
        api.MapPost("/grave-sites/{graveSiteId}/right", GrantRight).RequireAuthorization(Policies.Office);
        api.MapPost("/grave-rights/{rightId}/renewals", RenewRight).RequireAuthorization(Policies.Office);
        api.MapPost("/grave-rights/{rightId}/transfers", TransferRight).RequireAuthorization(Policies.Office);
        api.MapPost("/grave-rights/{rightId}/inheritances", InheritRight).RequireAuthorization(Policies.Office);
        api.MapPost("/grave-sites/{graveSiteId}/reservations", Reserve).RequireAuthorization(Policies.Office);
        api.MapPost("/reservations/{reservationId}/cancellations", CancelReservation).RequireAuthorization(Policies.Office);
        api.MapGet("/grave-sites/{graveSiteId}/documents", ListDocuments).RequireAuthorization(Policies.Office);
        api.MapPost("/grave-sites/{graveSiteId}/documents", AddDocument).RequireAuthorization(Policies.Office);
        api.MapGet("/documents/{documentId}", DownloadDocument).RequireAuthorization(Policies.Office);
    }

    private static void MapPortal(RouteGroupBuilder api)
    {
        api.MapGet("/me/grave-sites", MySites).RequireAuthorization(Policies.Holder);
        api.MapGet("/me/documents", MyDocuments).RequireAuthorization(Policies.Holder);
        api.MapGet("/me/documents/{documentId}", DownloadMine).RequireAuthorization(Policies.Holder);
        api.MapGet("/me/charges", MyCharges).RequireAuthorization(Policies.Holder);
        api.MapPost("/me/charges/{chargeId}/payments", Pay).RequireAuthorization(Policies.Holder);
        api.MapGet("/me/payments/{paymentId}", MyPayment).RequireAuthorization(Policies.Holder);
    }

    private static void MapRead(RouteGroupBuilder api)
    {
        api.MapGet("/cemeteries/{cemeteryId}/map", Map).RequireAuthorization(Policies.Office);
        api.MapGet("/grave-sites/{graveSiteId}/dossier", Dossier).RequireAuthorization(Policies.Field);
        api.MapGet("/search", Search).RequireAuthorization(Policies.Field);
        api.MapGet("/cemeteries/{cemeteryId}/entrances", ListEntrances).RequireAuthorization(Policies.Office);
        api.MapPost("/cemeteries/{cemeteryId}/entrances", PlaceEntrance).RequireAuthorization(Policies.Office);
        api.MapDelete("/entrances/{entranceId}", RemoveEntrance).RequireAuthorization(Policies.Office);
        api.MapGet("/cemeteries/{cemeteryId}/paths", ListPaths).RequireAuthorization(Policies.Office);
        api.MapPost("/cemeteries/{cemeteryId}/paths", DrawPath).RequireAuthorization(Policies.Office);
        api.MapDelete("/paths/{pathId}", RemovePath).RequireAuthorization(Policies.Office);
        api.MapGet("/work-orders/{workOrderId}/photos/{photoId}", Photo).RequireAuthorization(Policies.Field);
    }

    private static async Task<Ok<Page<HolderRecordView>>> ListHolders(
        string? q,
        string? cursor,
        int? limit,
        IQueryHandler<ListHolders, Page<HolderRecordView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListHolders(q, cursor, limit), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<HolderRecordView>> CreateHolder(
        HolderRecordBody body,
        ICommandHandler<CreateHolder, HolderRecordView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new CreateHolder(body.GivenName, body.FamilyName, body.Oib, body.Address, body.Email, body.Phone), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<HolderRecordView>> GetHolder(
        Guid holderId,
        IQueryHandler<GetHolder, HolderRecordView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetHolder(holderId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<HolderRecordView>> UpdateHolder(
        Guid holderId,
        HolderRecordBody body,
        ICommandHandler<UpdateHolder, HolderRecordView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new UpdateHolder(holderId, body.GivenName, body.FamilyName, body.Oib, body.Address, body.Email, body.Phone), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<InvitationIssued>> InviteHolder(
        Guid holderId,
        ICommandHandler<InviteHolder, InvitationIssued> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new InviteHolder(holderId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<HolderRecordView>> MyHolder(
        IQueryHandler<GetMyHolder, HolderRecordView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetMyHolder(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<SiteRightsView>> GetRight(
        Guid graveSiteId,
        IQueryHandler<GetSiteRights, SiteRightsView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetSiteRights(graveSiteId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveRightView>> GrantRight(
        Guid graveSiteId,
        GrantRightBody body,
        ICommandHandler<GrantRight, GraveRightView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GrantRight(graveSiteId, body.Perpetual, body.TermYears, body.Holders), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveRightView>> RenewRight(
        Guid rightId,
        RenewRightBody body,
        ICommandHandler<RenewRight, GraveRightView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new RenewRight(rightId, body.Years), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveRightView>> TransferRight(
        Guid rightId,
        ConveyRightBody body,
        ICommandHandler<TransferRight, GraveRightView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new TransferRight(rightId, body.On, body.Holders), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveRightView>> InheritRight(
        Guid rightId,
        ConveyRightBody body,
        ICommandHandler<InheritRight, GraveRightView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new InheritRight(rightId, body.On, body.Holders), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<ReservationView>> Reserve(
        Guid graveSiteId,
        ReservationBody body,
        ICommandHandler<ReserveSite, ReservationView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ReserveSite(graveSiteId, body.GivenName, body.FamilyName, body.Email, body.Until), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<SiteRightsView>> CancelReservation(
        Guid reservationId,
        ICommandHandler<CancelReservation, SiteRightsView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new CancelReservation(reservationId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<DocumentView>>> ListDocuments(
        Guid graveSiteId,
        IQueryHandler<ListSiteDocuments, IReadOnlyList<DocumentView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListSiteDocuments(graveSiteId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<DocumentView>> AddDocument(
        Guid graveSiteId,
        DocumentBody body,
        ICommandHandler<AddSiteDocument, DocumentView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new AddSiteDocument(graveSiteId, body.FileName, body.ContentType, body.ContentBase64, body.Kind), cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> DownloadDocument(
        Guid documentId,
        IQueryHandler<DownloadDocument, FileDownload> handler,
        CancellationToken cancellationToken) =>
        File(await handler.Handle(new DownloadDocument(documentId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<HolderSiteView>>> MySites(
        IQueryHandler<ListMyGraveSites, IReadOnlyList<HolderSiteView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListMyGraveSites(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<DocumentView>>> MyDocuments(
        IQueryHandler<ListMyDocuments, IReadOnlyList<DocumentView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListMyDocuments(), cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> DownloadMine(
        Guid documentId,
        IQueryHandler<DownloadMyDocument, FileDownload> handler,
        CancellationToken cancellationToken) =>
        File(await handler.Handle(new DownloadMyDocument(documentId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<ChargeView>>> MyCharges(
        IQueryHandler<ListMyCharges, IReadOnlyList<ChargeView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListMyCharges(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<PaymentView>> Pay(
        Guid chargeId,
        OnlinePaymentBody body,
        ICommandHandler<StartMyPayment, PaymentView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new StartMyPayment(chargeId, body.Provider), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<PaymentView>> MyPayment(
        Guid paymentId,
        IQueryHandler<GetMyPayment, PaymentView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetMyPayment(paymentId), cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> Map(
        Guid cemeteryId,
        HttpContext http,
        IQueryHandler<GetCemeteryMap, MapDocument?> handler,
        CancellationToken cancellationToken)
    {
        var match = http.Request.Headers.IfNoneMatch.ToString();
        var document = await handler.Handle(new GetCemeteryMap(cemeteryId, string.IsNullOrEmpty(match) ? null : match), cancellationToken).ConfigureAwait(false);
        http.Response.Headers.ETag = document?.Etag ?? match;
        return document is null
            ? Results.StatusCode(StatusCodes.Status304NotModified)
            : Results.Text(document.Json, "application/geo+json");
    }

    private static async Task<Ok<DossierView>> Dossier(
        Guid graveSiteId,
        IQueryHandler<GetDossier, DossierView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetDossier(graveSiteId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<SearchHit>>> Search(
        string? q,
        Guid? cemeteryId,
        IQueryHandler<SearchOffice, IReadOnlyList<SearchHit>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new SearchOffice(q ?? "", cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<EntranceView>>> ListEntrances(
        Guid cemeteryId,
        IQueryHandler<ListEntrances, IReadOnlyList<EntranceView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListEntrances(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<EntranceView>> PlaceEntrance(
        Guid cemeteryId,
        EntranceBody body,
        ICommandHandler<PlaceEntrance, EntranceView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new PlaceEntrance(cemeteryId, body.Name, body.Longitude, body.Latitude), cancellationToken).ConfigureAwait(false));

    private static async Task<NoContent> RemoveEntrance(
        Guid entranceId,
        ICommandHandler<RemoveEntrance, bool> handler,
        CancellationToken cancellationToken)
    {
        _ = await handler.Handle(new RemoveEntrance(entranceId), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<PathView>>> ListPaths(
        Guid cemeteryId,
        IQueryHandler<ListPaths, IReadOnlyList<PathView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListPaths(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<PathView>> DrawPath(
        Guid cemeteryId,
        PathBody body,
        ICommandHandler<DrawPath, PathView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new DrawPath(cemeteryId, body.Name, body.Line), cancellationToken).ConfigureAwait(false));

    private static async Task<NoContent> RemovePath(
        Guid pathId,
        ICommandHandler<RemovePath, bool> handler,
        CancellationToken cancellationToken)
    {
        _ = await handler.Handle(new RemovePath(pathId), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> Photo(
        Guid workOrderId,
        Guid photoId,
        IQueryHandler<GetWorkOrderPhoto, FileDownload> handler,
        CancellationToken cancellationToken) =>
        File(await handler.Handle(new GetWorkOrderPhoto(workOrderId, photoId), cancellationToken).ConfigureAwait(false));

    private static IResult File(FileDownload file) => Results.File(file.Content, file.ContentType, file.FileName);
}

public sealed record OnlinePaymentBody(string Provider);

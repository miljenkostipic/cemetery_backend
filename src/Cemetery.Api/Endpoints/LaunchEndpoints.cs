using Cemetery.Application.Abstractions;
using Cemetery.Application.Launch;
using Cemetery.Application.Register;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cemetery.Api.Endpoints;

public static class LaunchEndpoints
{
    public static RouteGroupBuilder MapLaunchEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/cemeteries/{cemeteryId}/interments", GetInterments).RequireAuthorization("tenant");
        api.MapGet("/cemeteries/{cemeteryId}/matica-links", GetLinks).RequireAuthorization("tenant");
        api.MapPost("/interments/{intermentId}/matica-links", PostLink).RequireAuthorization("tenant");
        api.MapGet("/integrations/webhooks", GetWebhooks).RequireAuthorization("tenant");
        api.MapPost("/integrations/webhooks", PostWebhook).RequireAuthorization("tenant");
        api.MapDelete("/integrations/webhooks/{webhookId}", DeleteWebhook).RequireAuthorization("tenant");
        api.MapGet("/gdpr/records", GetRecords).RequireAuthorization("tenant");
        api.MapPost("/gdpr/exports", PostExport).RequireAuthorization("tenant");
        return api;
    }

    private static async Task<Ok<IReadOnlyList<IntermentView>>> GetInterments(
        Guid cemeteryId,
        IQueryHandler<ListCemeteryInterments, IReadOnlyList<IntermentView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListCemeteryInterments(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<MaticaLinkView>>> GetLinks(
        Guid cemeteryId,
        IQueryHandler<ListMaticaLinks, IReadOnlyList<MaticaLinkView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListMaticaLinks(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<MaticaLinkView>> PostLink(
        Guid intermentId,
        MaticaLinkBody body,
        ICommandHandler<LinkDeathCertificate, MaticaLinkView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new LinkDeathCertificate(intermentId, body.DeathCertificateId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<WebhookView>>> GetWebhooks(
        IQueryHandler<ListWebhooks, IReadOnlyList<WebhookView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListWebhooks(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<WebhookCreated>> PostWebhook(
        WebhookBody body,
        ICommandHandler<AddWebhook, WebhookCreated> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new AddWebhook(body.Url, body.Event), cancellationToken).ConfigureAwait(false));

    private static async Task<NoContent> DeleteWebhook(
        Guid webhookId,
        ICommandHandler<RemoveWebhook, bool> handler,
        CancellationToken cancellationToken)
    {
        _ = await handler.Handle(new RemoveWebhook(webhookId), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IReadOnlyList<GdprRecordView>>> GetRecords(
        IQueryHandler<ListGdprRecords, IReadOnlyList<GdprRecordView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListGdprRecords(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GdprExportView>> PostExport(
        ICommandHandler<RequestGdprExport, GdprExportView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new RequestGdprExport(), cancellationToken).ConfigureAwait(false));
}

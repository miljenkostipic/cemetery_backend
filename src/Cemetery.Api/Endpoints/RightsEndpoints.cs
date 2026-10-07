using Cemetery.Application.Abstractions;
using Cemetery.Application.Rights;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cemetery.Api.Endpoints;

public static class RightsEndpoints
{
    public static RouteGroupBuilder MapRightsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/office-requests", List).RequireAuthorization("tenant");
        api.MapGet("/me/requests", Mine).RequireAuthorization("tenant");
        api.MapPost("/me/requests", Send).RequireAuthorization("tenant");
        return api;
    }

    private static async Task<Ok<IReadOnlyList<OfficeRequestView>>> List(
        IQueryHandler<ListOfficeRequests, IReadOnlyList<OfficeRequestView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListOfficeRequests(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<OfficeRequestView>>> Mine(
        IQueryHandler<ListMyOfficeRequests, IReadOnlyList<OfficeRequestView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListMyOfficeRequests(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<OfficeRequestView>> Send(
        OfficeRequestBody body,
        ICommandHandler<SendOfficeRequest, OfficeRequestView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new SendOfficeRequest(body.Message, body.GraveSiteId), cancellationToken).ConfigureAwait(false));
}

public sealed record OfficeRequestBody(string Message, Guid? GraveSiteId);

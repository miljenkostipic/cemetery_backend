using Cemetery.Api.Tenancy;
using Cemetery.Application.Abstractions;
using Cemetery.Application.Register;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cemetery.Api.Endpoints;

public static class RegisterEndpoints
{
    public static RouteGroupBuilder MapRegisterEndpoints(this RouteGroupBuilder api)
    {
        var cemeteries = api.MapGroup("/cemeteries").RequireAuthorization(Policies.Office);
        cemeteries.MapPost("/{cemeteryId}/deceased", RecordDeceased);
        cemeteries.MapGet("/{cemeteryId}/deceased", ListDeceased);
        cemeteries.MapPut("/{cemeteryId}/rest-period", RestPeriod);
        cemeteries.MapGet("/{cemeteryId}/positions", Positions);

        var people = api.MapGroup("/deceased").RequireAuthorization(Policies.Office);
        people.MapGet("/{deceasedId}", GetDeceased);
        people.MapPost("/{deceasedId}/corrections", Correct);
        people.MapDelete("/{deceasedId}", Remove);

        api.MapPost("/interments", RecordInterment).RequireAuthorization(Policies.Office);
        var interments = api.MapGroup("/interments").RequireAuthorization(Policies.Office);
        interments.MapPost("/{intermentId}/exhumations", Exhume);
        interments.MapPost("/{intermentId}/transfers", Transfer);

        api.MapGet("/grave-sites/{graveSiteId}/interments", SiteInterments).RequireAuthorization(Policies.Office);
        return api;
    }

    private static async Task<Ok<DeceasedView>> RecordDeceased(
        Guid cemeteryId,
        DeceasedBody body,
        ICommandHandler<RecordDeceased, DeceasedView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new RecordDeceased(cemeteryId, body.GivenName, body.FamilyName, body.BornOn, body.DiedOn), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<Page<DeceasedView>>> ListDeceased(
        Guid cemeteryId,
        string? q,
        Guid? sectionId,
        DateOnly? diedFrom,
        DateOnly? diedTo,
        bool? withoutSite,
        string? cursor,
        int? limit,
        IQueryHandler<ListDeceased, Page<DeceasedView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListDeceased(cemeteryId, q, sectionId, diedFrom, diedTo, withoutSite ?? false, cursor, limit), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<int>> RestPeriod(
        Guid cemeteryId,
        RestPeriodBody body,
        ICommandHandler<SetRestPeriod, int> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new SetRestPeriod(cemeteryId, body.Years), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<FreePositionView>>> Positions(
        Guid cemeteryId,
        Guid? graveSiteId,
        IQueryHandler<ListFreePositions, IReadOnlyList<FreePositionView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListFreePositions(cemeteryId, graveSiteId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<DeceasedView>> GetDeceased(
        Guid deceasedId,
        IQueryHandler<GetDeceased, DeceasedView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetDeceased(deceasedId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<DeceasedView>> Correct(
        Guid deceasedId,
        CorrectionBody body,
        ICommandHandler<CorrectDeceased, DeceasedView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new CorrectDeceased(deceasedId, body.GivenName, body.FamilyName, body.BornOn, body.DiedOn, body.Reason), cancellationToken).ConfigureAwait(false));

    private static async Task<NoContent> Remove(
        Guid deceasedId,
        ICommandHandler<RemoveDeceased, bool> handler,
        CancellationToken cancellationToken)
    {
        await handler.Handle(new RemoveDeceased(deceasedId), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<IntermentView>> RecordInterment(
        IntermentBody body,
        ICommandHandler<RecordInterment, IntermentView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new RecordInterment(body.DeceasedId, body.GraveSiteId, body.Position, body.Kind, body.BuriedOn), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IntermentView>> Exhume(
        Guid intermentId,
        ExhumationBody body,
        ICommandHandler<ExhumeInterment, IntermentView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ExhumeInterment(intermentId, body.On), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IntermentView>> Transfer(
        Guid intermentId,
        TransferBody body,
        ICommandHandler<TransferInterment, IntermentView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new TransferInterment(intermentId, body.GraveSiteId, body.Position, body.On), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<IntermentView>>> SiteInterments(
        Guid graveSiteId,
        IQueryHandler<ListSiteInterments, IReadOnlyList<IntermentView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListSiteInterments(graveSiteId), cancellationToken).ConfigureAwait(false));
}

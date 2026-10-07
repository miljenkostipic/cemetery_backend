using Cemetery.Application.Abstractions;
using Cemetery.Application.Layout;
using Cemetery.Application.Catalog;
using Cemetery.Application.Register;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cemetery.Api.Endpoints;

public static class PublicEndpoints
{
    public static RouteGroupBuilder MapPublicEndpoints(this RouteGroupBuilder api)
    {
        var catalog = api.MapGroup("/public").AllowAnonymous().RequireRateLimiting(PublicRateLimit.Policy);
        catalog.MapGet("/cemeteries", List);
        catalog.MapGet("/search", SearchAll);
        catalog.MapGet("/cemeteries/{slug}/{cemeteryId}", Map);
        catalog.MapGet("/cemeteries/{slug}/search", SearchOne);
        catalog.MapGet("/memorials/{deceasedId}", Memorial);

        api.MapGet("/organizations/visibility", ReadVisibility).RequireAuthorization("tenant");
        api.MapPut("/organizations/visibility", Publish).RequireAuthorization("tenant");
        api.MapDelete("/organizations/visibility", Withdraw).RequireAuthorization("tenant");
        api.MapPost("/grave-sites/{graveSiteId}/hide-from-public", Hide).RequireAuthorization("tenant");
        api.MapPost("/grave-sites/{graveSiteId}/show-on-public-map", Show).RequireAuthorization("tenant");
        api.MapPut("/deceased/{deceasedId}/memorial", Describe).RequireAuthorization("tenant");
        return api;
    }

    private static async Task<Ok<IReadOnlyList<PublicCemeterySummary>>> List(
        IQueryHandler<ListPublicCemeteries, IReadOnlyList<PublicCemeterySummary>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListPublicCemeteries(), cancellationToken).ConfigureAwait(false));

    private static Task<Ok<IReadOnlyList<PublicSearchHit>>> SearchAll(
        string name,
        int? yearFrom,
        int? yearTo,
        IQueryHandler<SearchPublic, IReadOnlyList<PublicSearchHit>> handler,
        CancellationToken cancellationToken) =>
        Search(null, name, yearFrom, yearTo, handler, cancellationToken);

    private static Task<Ok<IReadOnlyList<PublicSearchHit>>> SearchOne(
        string slug,
        string name,
        int? yearFrom,
        int? yearTo,
        IQueryHandler<SearchPublic, IReadOnlyList<PublicSearchHit>> handler,
        CancellationToken cancellationToken) =>
        Search(slug, name, yearFrom, yearTo, handler, cancellationToken);

    private static async Task<Ok<IReadOnlyList<PublicSearchHit>>> Search(
        string? slug,
        string name,
        int? yearFrom,
        int? yearTo,
        IQueryHandler<SearchPublic, IReadOnlyList<PublicSearchHit>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new SearchPublic(name, yearFrom, yearTo, slug), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<PublicMap>> Map(
        string slug,
        Guid cemeteryId,
        IQueryHandler<GetPublicMap, PublicMap> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetPublicMap(slug, cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<PublicMemorial>> Memorial(
        Guid deceasedId,
        IQueryHandler<GetMemorial, PublicMemorial> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetMemorial(deceasedId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<VisibilityView>> ReadVisibility(
        IQueryHandler<GetVisibility, VisibilityView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetVisibility(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<VisibilityView>> Publish(
        VisibilityBody body,
        ICommandHandler<PublishRegister, VisibilityView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new PublishRegister(body.HideRecentDeathsDays), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<VisibilityView>> Withdraw(
        ICommandHandler<WithdrawRegister, VisibilityView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new WithdrawRegister(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveSiteView>> Hide(
        Guid graveSiteId,
        ICommandHandler<HideGraveFromPublic, GraveSiteView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new HideGraveFromPublic(graveSiteId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveSiteView>> Show(
        Guid graveSiteId,
        ICommandHandler<ShowGraveOnPublicMap, GraveSiteView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ShowGraveOnPublicMap(graveSiteId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<DeceasedView>> Describe(
        Guid deceasedId,
        MemorialBody body,
        ICommandHandler<DescribeMemorial, DeceasedView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new DescribeMemorial(deceasedId, body.Epitaph, body.PhotoUrl), cancellationToken).ConfigureAwait(false));
}

public static class PublicRateLimit
{
    public const string Policy = "public";
    public const int PermitLimit = 30;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);
}

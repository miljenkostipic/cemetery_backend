using Cemetery.Application.Abstractions;
using Cemetery.Application.Layout;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cemetery.Api.Endpoints;

public static class LayoutEndpoints
{
    public static RouteGroupBuilder MapLayoutEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/cemeteries", Open).RequireAuthorization("tenant");
        api.MapGet("/cemeteries", List).RequireAuthorization("tenant");
        var cemeteries = api.MapGroup("/cemeteries").RequireAuthorization("tenant");
        cemeteries.MapGet("/{cemeteryId}", Get);
        cemeteries.MapPost("/{cemeteryId}/plan", Plan);
        cemeteries.MapPost("/{cemeteryId}/sections", AddSection);
        cemeteries.MapGet("/{cemeteryId}/sections", ListSections);
        cemeteries.MapGet("/{cemeteryId}/grave-sites", ListSites);

        var sections = api.MapGroup("/sections").RequireAuthorization("tenant");
        sections.MapDelete("/{sectionId}", DeleteSection);
        sections.MapPut("/{sectionId}/outline", SectionOutline);
        sections.MapPost("/{sectionId}/rows", AddRow);
        sections.MapGet("/{sectionId}/rows", ListRows);
        sections.MapPost("/{sectionId}/grave-sites", Generate);

        var sites = api.MapGroup("/grave-sites").RequireAuthorization("tenant");
        sites.MapPost("/merge", Merge);
        sites.MapPost("/undo-split", UndoSplit);
        sites.MapGet("/{graveSiteId}", GetSite);
        sites.MapPut("/{graveSiteId}/outline", SiteOutline);
        sites.MapPost("/{graveSiteId}/split", Split);
        sites.MapPost("/{graveSiteId}/close", Close);
        sites.MapPost("/{graveSiteId}/reopen", Reopen);
        return api;
    }

    private static async Task<Ok<CemeteryView>> Open(
        OpenCemetery command,
        ICommandHandler<OpenCemetery, CemeteryView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(command, cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<CemeteryView>>> List(
        IQueryHandler<ListCemeteries, IReadOnlyList<CemeteryView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListCemeteries(), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<CemeteryView>> Get(
        Guid cemeteryId,
        IQueryHandler<GetCemetery, CemeteryView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetCemetery(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<CemeteryView>> Plan(
        Guid cemeteryId,
        PlanBody body,
        ICommandHandler<SetCemeteryPlan, CemeteryView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new SetCemeteryPlan(cemeteryId, body.PlanImageUrl, body.PlanBounds), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<SectionView>> AddSection(
        Guid cemeteryId,
        SectionBody body,
        ICommandHandler<AddSection, SectionView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new AddSection(cemeteryId, body.Name, body.Code, body.Outline), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<SectionView>>> ListSections(
        Guid cemeteryId,
        IQueryHandler<ListSections, IReadOnlyList<SectionView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListSections(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<NoContent> DeleteSection(
        Guid sectionId,
        ICommandHandler<RemoveSection, bool> handler,
        CancellationToken cancellationToken)
    {
        _ = await handler.Handle(new RemoveSection(sectionId), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<SectionView>> SectionOutline(
        Guid sectionId,
        OutlineBody body,
        ICommandHandler<ReplaceSectionOutline, SectionView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ReplaceSectionOutline(sectionId, body.Outline), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveRowView>> AddRow(
        Guid sectionId,
        RowBody body,
        ICommandHandler<AddGraveRow, GraveRowView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new AddGraveRow(sectionId, body.Label), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<GraveRowView>>> ListRows(
        Guid sectionId,
        IQueryHandler<ListGraveRows, IReadOnlyList<GraveRowView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListGraveRows(sectionId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<GraveSiteView>>> Generate(
        Guid sectionId,
        GenerateBody body,
        ICommandHandler<GenerateGraveSites, IReadOnlyList<GraveSiteView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GenerateGraveSites(sectionId, body.Kind, body.Rows, body.Columns, body.OriginLongitude, body.OriginLatitude, body.WidthMeters, body.DepthMeters, body.GapMeters, body.RotationDegrees), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<GraveSiteView>>> ListSites(
        Guid cemeteryId,
        IQueryHandler<ListGraveSites, IReadOnlyList<GraveSiteView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListGraveSites(cemeteryId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveSiteView>> GetSite(
        Guid graveSiteId,
        IQueryHandler<GetGraveSite, GraveSiteView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new GetGraveSite(graveSiteId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveSiteView>> SiteOutline(
        Guid graveSiteId,
        OutlineBody body,
        ICommandHandler<ReplaceGraveSiteOutline, GraveSiteView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ReplaceGraveSiteOutline(graveSiteId, body.Outline), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<GraveSiteView>>> Split(
        Guid graveSiteId,
        ICommandHandler<SplitGraveSite, IReadOnlyList<GraveSiteView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new SplitGraveSite(graveSiteId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveSiteView>> Merge(
        MergeGraveSites command,
        ICommandHandler<MergeGraveSites, GraveSiteView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(command, cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveSiteView>> Close(
        Guid graveSiteId,
        ICommandHandler<CloseGraveSite, GraveSiteView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new CloseGraveSite(graveSiteId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveSiteView>> Reopen(
        Guid graveSiteId,
        ICommandHandler<ReopenGraveSite, GraveSiteView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ReopenGraveSite(graveSiteId), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveSiteView>> UndoSplit(
        UndoSplitBody body,
        ICommandHandler<UndoGraveSiteSplit, GraveSiteView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new UndoGraveSiteSplit(body.OriginalId, body.LeftId, body.RightId), cancellationToken).ConfigureAwait(false));
}

public sealed record PlanBody(string PlanImageUrl, IReadOnlyList<GeoPointView> PlanBounds);

public sealed record SectionBody(string Name, string Code, IReadOnlyList<GeoPointView>? Outline);

public sealed record OutlineBody(IReadOnlyList<GeoPointView> Outline);

public sealed record RowBody(string Label);

public sealed record UndoSplitBody(Guid OriginalId, Guid LeftId, Guid RightId);

public sealed record GenerateBody(
    string Kind,
    int Rows,
    int Columns,
    double OriginLongitude,
    double OriginLatitude,
    double WidthMeters,
    double DepthMeters,
    double GapMeters,
    double RotationDegrees);

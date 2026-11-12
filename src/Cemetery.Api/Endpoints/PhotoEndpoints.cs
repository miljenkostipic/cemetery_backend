using Cemetery.Api.Tenancy;
using Cemetery.Application.Abstractions;
using Cemetery.Application.Photos;
using Cemetery.Application.Rights;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Cemetery.Api.Endpoints;

/// <summary>Grave photos: office and field workers upload and view; only the office edits, picks the cover and deletes.</summary>
public static class PhotoEndpoints
{
    /// <summary>A photo id always serves the same bytes, so the browser may keep them for a year.</summary>
    public const string ImageCacheControl = "private, max-age=31536000, immutable";

    public static RouteGroupBuilder MapPhotoEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/grave-sites/{graveSiteId}/photos", Upload).RequireAuthorization(Policies.Field);
        api.MapGet("/grave-sites/{graveSiteId}/photos", List).RequireAuthorization(Policies.Field);
        api.MapGet("/grave-site-photos/{photoId}/image", Image).RequireAuthorization(Policies.Field);
        api.MapPut("/grave-site-photos/{photoId}", Update).RequireAuthorization(Policies.Office);
        api.MapPost("/grave-site-photos/{photoId}/cover", Cover).RequireAuthorization(Policies.Office);
        api.MapDelete("/grave-site-photos/{photoId}", Delete).RequireAuthorization(Policies.Office);
        return api;
    }

    private static async Task<Ok<GraveSitePhotoView>> Upload(
        Guid graveSiteId,
        GraveSitePhotoBody body,
        ICommandHandler<AddGraveSitePhoto, GraveSitePhotoView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new AddGraveSitePhoto(graveSiteId, body.ContentType, body.ContentBase64, body.Caption, body.TakenOn), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<IReadOnlyList<GraveSitePhotoView>>> List(
        Guid graveSiteId,
        IQueryHandler<ListGraveSitePhotos, IReadOnlyList<GraveSitePhotoView>> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new ListGraveSitePhotos(graveSiteId), cancellationToken).ConfigureAwait(false));

    private static async Task<IResult> Image(
        Guid photoId,
        string? size,
        HttpResponse response,
        IQueryHandler<GetGraveSitePhotoImage, FileDownload> handler,
        CancellationToken cancellationToken)
    {
        var file = await handler.Handle(new GetGraveSitePhotoImage(photoId, size), cancellationToken).ConfigureAwait(false);
        response.Headers.CacheControl = ImageCacheControl;
        return Results.File(file.Content, file.ContentType);
    }

    private static async Task<Ok<GraveSitePhotoView>> Update(
        Guid photoId,
        GraveSitePhotoUpdateBody body,
        ICommandHandler<UpdateGraveSitePhoto, GraveSitePhotoView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new UpdateGraveSitePhoto(photoId, body.Caption, body.TakenOn, body.ShowOnMemorial), cancellationToken).ConfigureAwait(false));

    private static async Task<Ok<GraveSitePhotoView>> Cover(
        Guid photoId,
        ICommandHandler<SetGraveSitePhotoCover, GraveSitePhotoView> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.Handle(new SetGraveSitePhotoCover(photoId), cancellationToken).ConfigureAwait(false));

    private static async Task<NoContent> Delete(
        Guid photoId,
        ICommandHandler<DeleteGraveSitePhoto, bool> handler,
        CancellationToken cancellationToken)
    {
        _ = await handler.Handle(new DeleteGraveSitePhoto(photoId), cancellationToken).ConfigureAwait(false);
        return TypedResults.NoContent();
    }
}

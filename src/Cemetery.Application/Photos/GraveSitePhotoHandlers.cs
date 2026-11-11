using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Application.Register;
using Cemetery.Application.Rights;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Photos;
using FluentValidation;

namespace Cemetery.Application.Photos;

public sealed record GraveSitePhotoBody(string ContentType, string ContentBase64, string? Caption = null, DateOnly? TakenOn = null);

public sealed record GraveSitePhotoUpdateBody(bool ShowOnMemorial, string? Caption = null, DateOnly? TakenOn = null);

public sealed record GraveSitePhotoView(
    Guid Id,
    Guid GraveSiteId,
    string? Caption,
    DateOnly? TakenOn,
    bool IsCover,
    bool ShowOnMemorial,
    int Width,
    int Height,
    DateTimeOffset CreatedAt,
    string ImageUrl,
    string ThumbnailUrl);

public static class GraveSitePhotoMaps
{
    public static string ImageUrl(Guid photoId) => "/api/v1/grave-site-photos/" + photoId + "/image";

    public static string ThumbnailUrl(Guid photoId) => ImageUrl(photoId) + "?size=thumb";

    public static GraveSitePhotoView ToView(GraveSitePhoto photo) =>
        new(photo.Id, photo.GraveSiteId, photo.Caption, photo.TakenOn, photo.IsCover, photo.ShowOnMemorial, photo.Width, photo.Height, photo.CreatedAt, ImageUrl(photo.Id), ThumbnailUrl(photo.Id));
}

public sealed record ListGraveSitePhotos(Guid GraveSiteId);

public sealed class ListGraveSitePhotosHandler(ICallerContext caller, ILayoutRepository layouts, IGraveSitePhotoRepository photos) : IQueryHandler<ListGraveSitePhotos, IReadOnlyList<GraveSitePhotoView>>
{
    public async Task<IReadOnlyList<GraveSitePhotoView>> Handle(ListGraveSitePhotos query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        _ = await layouts.FindGraveSiteAsync(query.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var rows = await photos.ListAsync(query.GraveSiteId, cancellationToken).ConfigureAwait(false);
        return rows.Select(GraveSitePhotoMaps.ToView).ToArray();
    }
}

public sealed record AddGraveSitePhoto(Guid GraveSiteId, string ContentType, string ContentBase64, string? Caption, DateOnly? TakenOn);

public sealed class AddGraveSitePhotoValidator : AbstractValidator<AddGraveSitePhoto>
{
    public AddGraveSitePhotoValidator() => RuleFor(command => command.GraveSiteId).NotEmpty().WithErrorCode("grave_site.not_found");
}

public sealed class AddGraveSitePhotoHandler(
    ICallerContext caller,
    ILayoutRepository layouts,
    IGraveSitePhotoRepository photos,
    IImageProcessor images,
    IFileStorage storage,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<AddGraveSitePhoto, GraveSitePhotoView>
{
    public async Task<GraveSitePhotoView> Handle(AddGraveSitePhoto command, CancellationToken cancellationToken)
    {
        _ = await layouts.FindGraveSiteAsync(command.GraveSiteId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("grave_site.not_found");
        var details = GraveSitePhotoDetails.Create(command.Caption, command.TakenOn, RegisterClock.Today(clock));
        var image = images.Process(GraveSitePhotoBytes.Decode(command.ContentType, command.ContentBase64));
        var files = await Store(image, cancellationToken).ConfigureAwait(false);
        var photo = GraveSitePhoto.Take(caller.OrganizationId, command.GraveSiteId, files, details, clock.GetUtcNow());
        var existing = await photos.ListAsync(command.GraveSiteId, cancellationToken).ConfigureAwait(false);
        if (existing.Count == 0)
            photo.MarkCover();
        await photos.AddAsync(photo, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return GraveSitePhotoMaps.ToView(photo);
    }

    private async Task<GraveSitePhotoFiles> Store(ProcessedImage image, CancellationToken cancellationToken)
    {
        var full = await storage.PutAsync(caller.OrganizationId, image.Full, cancellationToken).ConfigureAwait(false);
        var thumb = await storage.PutAsync(caller.OrganizationId, image.Thumbnail, cancellationToken).ConfigureAwait(false);
        return new GraveSitePhotoFiles(full, thumb, image.Width, image.Height);
    }
}

public sealed record GetGraveSitePhotoImage(Guid PhotoId, string? Size);

public sealed class GetGraveSitePhotoImageHandler(ICallerContext caller, IGraveSitePhotoRepository photos, IFileStorage storage) : IQueryHandler<GetGraveSitePhotoImage, FileDownload>
{
    public async Task<FileDownload> Handle(GetGraveSitePhotoImage query, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var thumbnail = Thumbnail(query.Size);
        var photo = await photos.FindAsync(query.PhotoId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("photo.not_found");
        var key = thumbnail ? photo.ThumbFileId : photo.FullFileId;
        var content = await storage.OpenAsync(key, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("photo.not_found");
        return new FileDownload(content, "image/jpeg", thumbnail ? "photo-thumb.jpg" : "photo.jpg");
    }

    private static bool Thumbnail(string? size) => size switch
    {
        null or "" or "full" => false,
        "thumb" => true,
        _ => throw new DomainRuleException("photo.size_invalid"),
    };
}

public sealed record UpdateGraveSitePhoto(Guid PhotoId, string? Caption, DateOnly? TakenOn, bool ShowOnMemorial);

public sealed class UpdateGraveSitePhotoValidator : AbstractValidator<UpdateGraveSitePhoto>
{
    public UpdateGraveSitePhotoValidator() => RuleFor(command => command.PhotoId).NotEmpty().WithErrorCode("photo.not_found");
}

public sealed class UpdateGraveSitePhotoHandler(ICallerContext caller, IGraveSitePhotoRepository photos, IUnitOfWork unitOfWork, TimeProvider clock) : ICommandHandler<UpdateGraveSitePhoto, GraveSitePhotoView>
{
    public async Task<GraveSitePhotoView> Handle(UpdateGraveSitePhoto command, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var photo = await photos.FindAsync(command.PhotoId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("photo.not_found");
        photo.Describe(GraveSitePhotoDetails.Create(command.Caption, command.TakenOn, RegisterClock.Today(clock)));
        if (command.ShowOnMemorial)
            photo.ShowOnMemorialPage();
        else
            photo.HideFromMemorialPage();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return GraveSitePhotoMaps.ToView(photo);
    }
}

public sealed record SetGraveSitePhotoCover(Guid PhotoId);

public sealed class SetGraveSitePhotoCoverValidator : AbstractValidator<SetGraveSitePhotoCover>
{
    public SetGraveSitePhotoCoverValidator() => RuleFor(command => command.PhotoId).NotEmpty().WithErrorCode("photo.not_found");
}

public sealed class SetGraveSitePhotoCoverHandler(ICallerContext caller, IGraveSitePhotoRepository photos, IUnitOfWork unitOfWork) : ICommandHandler<SetGraveSitePhotoCover, GraveSitePhotoView>
{
    public async Task<GraveSitePhotoView> Handle(SetGraveSitePhotoCover command, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var photo = await photos.FindAsync(command.PhotoId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("photo.not_found");
        var siblings = await photos.ListAsync(photo.GraveSiteId, cancellationToken).ConfigureAwait(false);
        GraveSitePhoto.ChooseCover(siblings.Where(item => item.Id != photo.Id), photo);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return GraveSitePhotoMaps.ToView(photo);
    }
}

public sealed record DeleteGraveSitePhoto(Guid PhotoId);

public sealed class DeleteGraveSitePhotoValidator : AbstractValidator<DeleteGraveSitePhoto>
{
    public DeleteGraveSitePhotoValidator() => RuleFor(command => command.PhotoId).NotEmpty().WithErrorCode("photo.not_found");
}

public sealed class DeleteGraveSitePhotoHandler(ICallerContext caller, IGraveSitePhotoRepository photos, IFileStorage storage, IUnitOfWork unitOfWork) : ICommandHandler<DeleteGraveSitePhoto, bool>
{
    public async Task<bool> Handle(DeleteGraveSitePhoto command, CancellationToken cancellationToken)
    {
        _ = caller.OrganizationId;
        var photo = await photos.FindAsync(command.PhotoId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("photo.not_found");
        await photos.RemoveAsync(photo, cancellationToken).ConfigureAwait(false);
        await storage.DeleteAsync(photo.FullFileId, cancellationToken).ConfigureAwait(false);
        await storage.DeleteAsync(photo.ThumbFileId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}

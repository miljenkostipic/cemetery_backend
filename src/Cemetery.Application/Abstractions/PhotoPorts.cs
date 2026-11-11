using Cemetery.Domain.Photos;

namespace Cemetery.Application.Abstractions;

/// <summary>Turns an uploaded image into upright, metadata-free JPEGs. Refuses anything it cannot decode with <c>photo.type_invalid</c>.</summary>
public interface IImageProcessor
{
    ProcessedImage Process(byte[] content);
}

/// <summary>The full-size and thumbnail JPEGs; <see cref="Width"/> and <see cref="Height"/> describe the full-size one.</summary>
public sealed record ProcessedImage(byte[] Full, byte[] Thumbnail, int Width, int Height);

public interface IGraveSitePhotoRepository
{
    Task AddAsync(GraveSitePhoto photo, CancellationToken cancellationToken);

    Task<GraveSitePhoto?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The site's photos, cover first, then newest first.</summary>
    Task<IReadOnlyList<GraveSitePhoto>> ListAsync(Guid graveSiteId, CancellationToken cancellationToken);

    Task<GraveSitePhoto?> FindCoverAsync(Guid graveSiteId, CancellationToken cancellationToken);

    Task RemoveAsync(GraveSitePhoto photo, CancellationToken cancellationToken);
}

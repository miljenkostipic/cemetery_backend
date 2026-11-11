using Cemetery.Application.Abstractions;
using Cemetery.Domain.Photos;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Infrastructure.Persistence;

internal sealed class GraveSitePhotoRepository(CemeteryDbContext db) : IGraveSitePhotoRepository
{
    public Task AddAsync(GraveSitePhoto photo, CancellationToken cancellationToken)
    {
        db.GraveSitePhotos.Add(photo);
        return Task.CompletedTask;
    }

    public Task<GraveSitePhoto?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.GraveSitePhotos.FirstOrDefaultAsync(photo => photo.Id == id, cancellationToken);

    public async Task<IReadOnlyList<GraveSitePhoto>> ListAsync(Guid graveSiteId, CancellationToken cancellationToken) =>
        await db.GraveSitePhotos.Where(photo => photo.GraveSiteId == graveSiteId)
            .OrderByDescending(photo => photo.IsCover)
            .ThenByDescending(photo => photo.CreatedAt)
            .ThenByDescending(photo => photo.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<GraveSitePhoto?> FindCoverAsync(Guid graveSiteId, CancellationToken cancellationToken) =>
        db.GraveSitePhotos.Where(photo => photo.GraveSiteId == graveSiteId && photo.IsCover)
            .OrderByDescending(photo => photo.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task RemoveAsync(GraveSitePhoto photo, CancellationToken cancellationToken)
    {
        db.GraveSitePhotos.Remove(photo);
        return Task.CompletedTask;
    }
}

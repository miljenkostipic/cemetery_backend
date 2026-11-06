using System.Globalization;
using Cemetery.Application.Abstractions;
using Cemetery.Domain.Rights;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Infrastructure.Persistence;

internal sealed class OfficeRequestRepository(CemeteryDbContext db) : IOfficeRequestRepository
{
    public Task AddAsync(OfficeRequest request, CancellationToken cancellationToken)
    {
        db.OfficeRequests.Add(request);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<OfficeRequest>> ListAsync(CancellationToken cancellationToken) =>
        await db.OfficeRequests.OrderByDescending(entity => entity.CreatedOn).ThenByDescending(entity => entity.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<OfficeRequest>> ListForAuthorAsync(Guid authorId, CancellationToken cancellationToken) =>
        await db.OfficeRequests.Where(entity => entity.AuthorId == authorId).OrderByDescending(entity => entity.CreatedOn).ThenByDescending(entity => entity.Id).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<Page<OfficeRequest>> PageAsync(string? query, PageRequest page, CancellationToken cancellationToken)
    {
        var size = Pages.Size(page.Limit);
        var rows = db.OfficeRequests.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
            rows = rows.Where(request => request.Message.Contains(query));
        var cursor = KeysetCursor.Decode(page.Cursor);
        if (cursor is { } key && DateOnly.TryParse(key.Sort, out var on))
            rows = rows.Where(request => request.CreatedOn < on || (request.CreatedOn == on && request.Id.CompareTo(key.Id) < 0));
        var taken = await rows.OrderByDescending(request => request.CreatedOn).ThenByDescending(request => request.Id).Take(size + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        return KeysetPage.Pack(taken, size, request => request.CreatedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), request => request.Id);
    }
}

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
}

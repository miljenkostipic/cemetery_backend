using Cemetery.Application.Abstractions;
using Cemetery.Domain.Register;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Infrastructure.Persistence;

internal sealed class RegisterRepository(CemeteryDbContext db) : IRegisterRepository
{
    public Task AddDeceasedAsync(Deceased deceased, CancellationToken cancellationToken)
    {
        db.Deceased.Add(deceased);
        return Task.CompletedTask;
    }

    public Task<Deceased?> FindDeceasedAsync(Guid id, CancellationToken cancellationToken) =>
        db.Deceased.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Deceased>> ListDeceasedAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.Deceased.Where(entity => entity.CemeteryId == cemeteryId && entity.RemovedAt == null).OrderBy(entity => entity.FamilyName).ThenBy(entity => entity.GivenName).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task AddRevisionAsync(DeceasedRevision revision, CancellationToken cancellationToken)
    {
        db.DeceasedRevisions.Add(revision);
        return Task.CompletedTask;
    }

    public Task AddIntermentAsync(Interment interment, CancellationToken cancellationToken)
    {
        db.Interments.Add(interment);
        return Task.CompletedTask;
    }

    public Task<Interment?> FindIntermentAsync(Guid id, CancellationToken cancellationToken) =>
        db.Interments.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Interment>> ListIntermentsBySiteAsync(Guid graveSiteId, CancellationToken cancellationToken) =>
        await db.Interments.Where(entity => entity.GraveSiteId == graveSiteId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Interment>> ListIntermentsByCemeteryAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.Interments.Where(entity => entity.CemeteryId == cemeteryId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Interment>> ListIntermentsByDeceasedAsync(Guid deceasedId, CancellationToken cancellationToken) =>
        await db.Interments.Where(entity => entity.DeceasedId == deceasedId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task AddAuditAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        db.AuditEntries.Add(entry);
        return Task.CompletedTask;
    }
}

using Cemetery.Application.Abstractions;
using Cemetery.Domain.Layout;
using Microsoft.EntityFrameworkCore;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Infrastructure.Persistence;

internal sealed class LayoutRepository(CemeteryDbContext db) : ILayoutRepository
{
    public Task AddCemeteryAsync(CemeteryPlace cemetery, CancellationToken cancellationToken)
    {
        db.Cemeteries.Add(cemetery);
        return Task.CompletedTask;
    }

    public Task<CemeteryPlace?> FindCemeteryAsync(Guid id, CancellationToken cancellationToken) =>
        db.Cemeteries.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CemeteryPlace>> ListCemeteriesAsync(CancellationToken cancellationToken) =>
        await db.Cemeteries.OrderBy(entity => entity.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task AddSectionAsync(Section section, CancellationToken cancellationToken)
    {
        db.Sections.Add(section);
        return Task.CompletedTask;
    }

    public Task<Section?> FindSectionAsync(Guid id, CancellationToken cancellationToken) =>
        db.Sections.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Section>> ListSectionsAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.Sections.Where(entity => entity.CemeteryId == cemeteryId).OrderBy(entity => entity.Code).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<bool> SectionCodeExistsAsync(Guid cemeteryId, string code, CancellationToken cancellationToken) =>
        db.Sections.AnyAsync(entity => entity.CemeteryId == cemeteryId && entity.Code == code, cancellationToken);

    public Task AddRowAsync(GraveRow row, CancellationToken cancellationToken)
    {
        db.GraveRows.Add(row);
        return Task.CompletedTask;
    }

    public Task<int> CountRowsAsync(Guid sectionId, CancellationToken cancellationToken) =>
        db.GraveRows.CountAsync(entity => entity.SectionId == sectionId, cancellationToken);

    public async Task<IReadOnlyList<GraveRow>> ListRowsAsync(Guid sectionId, CancellationToken cancellationToken) =>
        await db.GraveRows.Where(entity => entity.SectionId == sectionId).OrderBy(entity => entity.Label).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task AddGraveSiteAsync(GraveSite site, CancellationToken cancellationToken)
    {
        db.GraveSites.Add(site);
        return Task.CompletedTask;
    }

    public Task<GraveSite?> FindGraveSiteAsync(Guid id, CancellationToken cancellationToken) =>
        db.GraveSites.FirstOrDefaultAsync(entity => entity.Id == id, cancellationToken);

    public async Task<IReadOnlyList<GraveSite>> ListByCemeteryAsync(Guid cemeteryId, CancellationToken cancellationToken) =>
        await db.GraveSites.Where(entity => entity.CemeteryId == cemeteryId).OrderBy(entity => entity.Code).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<GraveSite>> ListOpenInSectionAsync(Guid sectionId, CancellationToken cancellationToken) =>
        await db.GraveSites.Where(entity => entity.SectionId == sectionId && !entity.Closed).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<int> CountGraveSitesAsync(CancellationToken cancellationToken) => db.GraveSites.CountAsync(cancellationToken);

    public async Task<int> NextSequenceAsync(Guid cemeteryId, CancellationToken cancellationToken)
    {
        var count = await db.GraveSites.CountAsync(entity => entity.CemeteryId == cemeteryId, cancellationToken).ConfigureAwait(false);
        return count + 1;
    }
}

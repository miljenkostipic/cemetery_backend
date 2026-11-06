using System.Globalization;
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

    public async Task<Page<Deceased>> PageDeceasedAsync(Guid cemeteryId, DeceasedFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var size = Pages.Size(page.Limit);
        var query = db.Deceased.Where(person => person.CemeteryId == cemeteryId && person.RemovedAt == null);
        query = Apply(query, filter);
        var cursor = KeysetCursor.Decode(page.Cursor);
        if (cursor is { } key)
            query = query.Where(person => person.FamilyName.CompareTo(key.Sort) > 0 || (person.FamilyName == key.Sort && person.Id.CompareTo(key.Id) > 0));
        var rows = await query.OrderBy(person => person.FamilyName).ThenBy(person => person.GivenName).ThenBy(person => person.Id).Take(size + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        return KeysetPage.Pack(rows, size, person => person.FamilyName, person => person.Id);
    }

    public async Task<Page<Interment>> PageIntermentsAsync(Guid cemeteryId, IntermentFilter filter, PageRequest page, CancellationToken cancellationToken)
    {
        var size = Pages.Size(page.Limit);
        var query = db.Interments.Where(item => item.CemeteryId == cemeteryId);
        if (filter.GraveSiteId is Guid siteId)
            query = query.Where(item => item.GraveSiteId == siteId);
        if (filter.BuriedFrom is DateOnly from)
            query = query.Where(item => item.BuriedOn >= from);
        if (filter.BuriedTo is DateOnly to)
            query = query.Where(item => item.BuriedOn <= to);
        var cursor = KeysetCursor.Decode(page.Cursor);
        if (cursor is { } key && DateOnly.TryParse(key.Sort, out var on))
            query = query.Where(item => item.BuriedOn > on || (item.BuriedOn == on && item.Id.CompareTo(key.Id) > 0));
        var rows = await query.OrderBy(item => item.BuriedOn).ThenBy(item => item.Id).Take(size + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        return KeysetPage.Pack(rows, size, item => item.BuriedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), item => item.Id);
    }

    private IQueryable<Deceased> Apply(IQueryable<Deceased> query, DeceasedFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var text = filter.Query.Trim();
            query = query.Where(person => CemeteryDbContext.Fold(person.GivenName + " " + person.FamilyName).Contains(CemeteryDbContext.Fold(text)));
        }

        if (filter.DiedFrom is DateOnly from)
            query = query.Where(person => person.DiedOn >= from);
        if (filter.DiedTo is DateOnly to)
            query = query.Where(person => person.DiedOn <= to);
        if (filter.WithoutSite)
            query = query.Where(person => !db.Interments.Any(item => item.DeceasedId == person.Id && item.ExhumedOn == null && item.TransferredOn == null && item.SupersededOn == null));
        if (filter.SectionId is Guid sectionId)
            query = query.Where(person => db.Interments.Any(item => item.DeceasedId == person.Id && item.ExhumedOn == null && item.TransferredOn == null && item.SupersededOn == null && db.GraveSites.Any(site => site.Id == item.GraveSiteId && site.SectionId == sectionId)));
        return query;
    }
}

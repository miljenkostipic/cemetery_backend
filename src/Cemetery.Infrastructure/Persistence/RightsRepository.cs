using Cemetery.Application.Abstractions;
using Cemetery.Domain.Billing;
using Cemetery.Domain.Rights;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Infrastructure.Persistence;

internal sealed class RightsRepository(CemeteryDbContext db) : IRightsRepository
{
    public Task AddHolderAsync(Holder holder, CancellationToken cancellationToken)
    {
        db.Holders.Add(holder);
        return Task.CompletedTask;
    }

    public Task<Holder?> FindHolderAsync(Guid id, CancellationToken cancellationToken) =>
        db.Holders.FirstOrDefaultAsync(holder => holder.Id == id, cancellationToken);

    public Task<Holder?> FindHolderByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return db.Holders.FirstOrDefaultAsync(holder => holder.Email == normalized, cancellationToken);
    }

    public Task<Holder?> FindHolderByUserAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Holders.FirstOrDefaultAsync(holder => holder.UserId == userId, cancellationToken);

    public async Task<Page<Holder>> PageHoldersAsync(string? query, PageRequest page, CancellationToken cancellationToken)
    {
        var size = Pages.Size(page.Limit);
        var rows = db.Holders.AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var text = query.Trim();
            rows = rows.Where(holder => CemeteryDbContext.Fold(holder.GivenName + " " + holder.FamilyName).Contains(CemeteryDbContext.Fold(text)) || (holder.Oib != null && holder.Oib.Contains(text)));
        }

        var cursor = KeysetCursor.Decode(page.Cursor);
        if (cursor is { } key)
            rows = rows.Where(holder => holder.FamilyName.CompareTo(key.Sort) > 0 || (holder.FamilyName == key.Sort && holder.Id.CompareTo(key.Id) > 0));
        var taken = await rows.OrderBy(holder => holder.FamilyName).ThenBy(holder => holder.GivenName).ThenBy(holder => holder.Id).Take(size + 1).ToListAsync(cancellationToken).ConfigureAwait(false);
        return KeysetPage.Pack(taken, size, holder => holder.FamilyName, holder => holder.Id);
    }

    public async Task LinkHolderAsync(string email, Guid userId, CancellationToken cancellationToken)
    {
        var matches = await db.Holders.Where(holder => holder.Email == email).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var holder in matches)
            holder.Link(userId);
    }

    public Task AddRightAsync(GraveRight right, CancellationToken cancellationToken)
    {
        db.GraveRights.Add(right);
        return Task.CompletedTask;
    }

    public Task<GraveRight?> FindRightAsync(Guid id, CancellationToken cancellationToken) =>
        db.GraveRights.Include(right => right.Shares).FirstOrDefaultAsync(right => right.Id == id, cancellationToken);

    public Task<GraveRight?> FindActiveRightAsync(Guid graveSiteId, CancellationToken cancellationToken) =>
        db.GraveRights.Include(right => right.Shares).FirstOrDefaultAsync(right => right.GraveSiteId == graveSiteId && right.Status == GraveRightStatus.Active, cancellationToken);

    public async Task<IReadOnlyList<GraveRight>> ListRightsForHolderAsync(Guid holderId, CancellationToken cancellationToken) =>
        await db.GraveRights.Include(right => right.Shares).Where(right => right.Shares.Any(share => share.HolderId == holderId)).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<BilledHolder>>> ActiveBillsAsync(Guid cemeteryId, CancellationToken cancellationToken)
    {
        var siteIds = db.GraveSites.Where(site => site.CemeteryId == cemeteryId).Select(site => site.Id);
        var rights = await db.GraveRights.Include(right => right.Shares)
            .Where(right => right.Status == GraveRightStatus.Active && siteIds.Contains(right.GraveSiteId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var holders = await Holders(rights, cancellationToken).ConfigureAwait(false);
        return Bills(rights, holders);
    }

    public Task AddReservationAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        db.Reservations.Add(reservation);
        return Task.CompletedTask;
    }

    public Task<Reservation?> FindReservationAsync(Guid id, CancellationToken cancellationToken) =>
        db.Reservations.FirstOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);

    public Task<Reservation?> FindActiveReservationAsync(Guid graveSiteId, CancellationToken cancellationToken) =>
        db.Reservations.FirstOrDefaultAsync(reservation => reservation.GraveSiteId == graveSiteId && reservation.Active, cancellationToken);

    public Task AddDocumentAsync(SiteDocument document, CancellationToken cancellationToken)
    {
        db.SiteDocuments.Add(document);
        return Task.CompletedTask;
    }

    public Task<SiteDocument?> FindDocumentAsync(Guid id, CancellationToken cancellationToken) =>
        db.SiteDocuments.FirstOrDefaultAsync(document => document.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SiteDocument>> ListDocumentsAsync(Guid graveSiteId, CancellationToken cancellationToken) =>
        await db.SiteDocuments.Where(document => document.GraveSiteId == graveSiteId).OrderBy(document => document.FileName).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<SiteDocument>> ListDocumentsForSitesAsync(IReadOnlyCollection<Guid> graveSiteIds, CancellationToken cancellationToken) =>
        await db.SiteDocuments.Where(document => graveSiteIds.Contains(document.GraveSiteId)).OrderBy(document => document.FileName).ToListAsync(cancellationToken).ConfigureAwait(false);

    private async Task<Dictionary<Guid, Holder>> Holders(List<GraveRight> rights, CancellationToken cancellationToken)
    {
        var ids = rights.SelectMany(right => right.Shares).Select(share => share.HolderId).Distinct().ToArray();
        return await db.Holders.Where(holder => ids.Contains(holder.Id)).ToDictionaryAsync(holder => holder.Id, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<Guid, IReadOnlyList<BilledHolder>> Bills(List<GraveRight> rights, Dictionary<Guid, Holder> holders)
    {
        var bills = new Dictionary<Guid, IReadOnlyList<BilledHolder>>();
        foreach (var right in rights)
            bills[right.GraveSiteId] = People(right, holders);
        return bills;
    }

    private static BilledHolder[] People(GraveRight right, Dictionary<Guid, Holder> holders)
    {
        var people = new List<BilledHolder>();
        foreach (var share in right.Shares)
        {
            if (holders.TryGetValue(share.HolderId, out var holder))
                people.Add(new BilledHolder(holder.Id, holder.DisplayName, share.Percent));
        }

        return people.ToArray();
    }
}

internal sealed class DatabaseFileStorage(CemeteryDbContext db) : IFileStorage
{
    public Task<string> PutAsync(Guid tenantId, byte[] content, CancellationToken cancellationToken)
    {
        var file = StoredFile.Keep(tenantId, content);
        db.StoredFiles.Add(file);
        return Task.FromResult(file.Id.ToString());
    }

    public async Task<byte[]?> OpenAsync(string key, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(key, out var id))
            return null;

        var file = await db.StoredFiles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken).ConfigureAwait(false);
        return file?.Content;
    }
}

internal sealed class NoOpVirusScan : IVirusScan
{
    public Task ScanAsync(byte[] content, CancellationToken cancellationToken) => Task.CompletedTask;
}

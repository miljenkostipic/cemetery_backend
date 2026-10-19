using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using Cemetery.Domain.Launch;
using Cemetery.Domain.Operations;
using Cemetery.Domain.Register;
using Cemetery.Domain.Rights;
using Cemetery.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Infrastructure.Persistence;

public sealed class CemeteryDbContext(DbContextOptions<CemeteryDbContext> options, ITenantContext tenant)
    : IdentityUserContext<ApplicationUser, Guid>(options), IUnitOfWork
{
    public Guid CurrentTenant => tenant.OrganizationId ?? Guid.Empty;

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<CemeteryPlace> Cemeteries => Set<CemeteryPlace>();

    public DbSet<Section> Sections => Set<Section>();

    public DbSet<GraveRow> GraveRows => Set<GraveRow>();

    public DbSet<GraveSite> GraveSites => Set<GraveSite>();

    public DbSet<Deceased> Deceased => Set<Deceased>();

    public DbSet<DeceasedRevision> DeceasedRevisions => Set<DeceasedRevision>();

    public DbSet<Interment> Interments => Set<Interment>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<OfficeRequest> OfficeRequests => Set<OfficeRequest>();

    public DbSet<Funeral> Funerals => Set<Funeral>();

    public DbSet<ChapelBooking> ChapelBookings => Set<ChapelBooking>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<MonumentPermit> MonumentPermits => Set<MonumentPermit>();

    public DbSet<WorkOrderPhoto> WorkOrderPhotos => Set<WorkOrderPhoto>();

    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();

    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    public DbSet<MaticaLink> MaticaLinks => Set<MaticaLink>();

    public DbSet<GdprExport> GdprExports => Set<GdprExport>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("cemetery");
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CemeteryDbContext).Assembly);
        builder.Entity<Membership>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<Invitation>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<Organization>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.Id == CurrentTenant);
        builder.Entity<CemeteryPlace>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<Section>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<GraveRow>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<GraveSite>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<Deceased>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<DeceasedRevision>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<Interment>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<AuditEntry>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<OfficeRequest>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        FilterOperations(builder);
        FilterLaunch(builder);
    }

    private void FilterOperations(ModelBuilder builder)
    {
        builder.Entity<Funeral>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<ChapelBooking>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<WorkOrder>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<MonumentPermit>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<WorkOrderPhoto>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
    }

    private void FilterLaunch(ModelBuilder builder)
    {
        builder.Entity<WebhookSubscription>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<WebhookDelivery>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<MaticaLink>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<GdprExport>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
    }

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        RejectUnstampedTenants();
        try
        {
            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (UniqueViolation.ToErrorCode(exception) is string code)
        {
            throw new ConflictException(code);
        }
    }

    private void RejectUnstampedTenants()
    {
        foreach (var entry in ChangeTracker.Entries<TenantEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                throw new DomainRuleException("tenant.required");
        }
    }
}

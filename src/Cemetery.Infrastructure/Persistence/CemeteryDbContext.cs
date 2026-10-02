using Cemetery.Application.Abstractions;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Identity;
using Cemetery.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Cemetery.Infrastructure.Persistence;

public sealed class CemeteryDbContext(DbContextOptions<CemeteryDbContext> options, ITenantContext tenant)
    : IdentityUserContext<ApplicationUser, Guid>(options), IUnitOfWork
{
    public Guid CurrentTenant => tenant.OrganizationId ?? Guid.Empty;

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("cemetery");
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(CemeteryDbContext).Assembly);
        builder.Entity<Membership>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<Invitation>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.TenantId == CurrentTenant);
        builder.Entity<Organization>().HasQueryFilter(entity => CurrentTenant != Guid.Empty && entity.Id == CurrentTenant);
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

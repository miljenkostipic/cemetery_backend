using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Infrastructure.Persistence;

public sealed class DeceasedConfiguration : IEntityTypeConfiguration<Deceased>
{
    public void Configure(EntityTypeBuilder<Deceased> builder)
    {
        builder.ToTable("deceased");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        builder.Property(entity => entity.GivenName).HasMaxLength(PersonName.MaxLength);
        builder.Property(entity => entity.FamilyName).HasMaxLength(PersonName.MaxLength);
        builder.HasIndex(entity => new { entity.TenantId, entity.CemeteryId });
        builder.HasOne<CemeteryPlace>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.CemeteryId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DeceasedRevisionConfiguration : IEntityTypeConfiguration<DeceasedRevision>
{
    public void Configure(EntityTypeBuilder<DeceasedRevision> builder)
    {
        builder.ToTable("deceased_revisions");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.GivenName).HasMaxLength(PersonName.MaxLength);
        builder.Property(entity => entity.FamilyName).HasMaxLength(PersonName.MaxLength);
        builder.Property(entity => entity.Reason).HasMaxLength(CorrectionReason.MaxLength);
        builder.HasIndex(entity => new { entity.TenantId, entity.DeceasedId, entity.Version }).IsUnique().HasDatabaseName("deceased_revisions_version_key");
        builder.HasOne<Deceased>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.DeceasedId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class IntermentConfiguration : IEntityTypeConfiguration<Interment>
{
    public void Configure(EntityTypeBuilder<Interment> builder)
    {
        builder.ToTable("interments");
        builder.HasKey(entity => entity.Id);
        builder.Ignore(entity => entity.Occupies);
        builder.Property(entity => entity.Kind)
            .HasConversion(kind => IntermentKinds.ToCode(kind), code => IntermentKinds.Parse(code))
            .HasMaxLength(16);
        builder.HasIndex(entity => new { entity.TenantId, entity.GraveSiteId, entity.Position })
            .IsUnique()
            .HasFilter("exhumed_on IS NULL AND transferred_on IS NULL AND superseded_on IS NULL")
            .HasDatabaseName("interments_open_position_key");
        builder.HasIndex(entity => new { entity.TenantId, entity.DeceasedId })
            .IsUnique()
            .HasFilter("exhumed_on IS NULL AND transferred_on IS NULL AND superseded_on IS NULL")
            .HasDatabaseName("interments_open_deceased_key");
        Relate(builder);
    }

    private static void Relate(EntityTypeBuilder<Interment> builder)
    {
        builder.HasOne<Deceased>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.DeceasedId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GraveSite>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.GraveSiteId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CemeteryPlace>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.CemeteryId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("audit_entries");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Action).HasMaxLength(64);
        builder.Property(entity => entity.Reason).HasMaxLength(CorrectionReason.MaxLength);
        builder.HasIndex(entity => new { entity.TenantId, entity.SubjectId });
    }
}

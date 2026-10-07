using Cemetery.Domain.Layout;
using Cemetery.Domain.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Infrastructure.Persistence;

public sealed class CemeteryConfiguration : IEntityTypeConfiguration<CemeteryPlace>
{
    public void Configure(EntityTypeBuilder<CemeteryPlace> builder)
    {
        builder.ToTable("cemeteries");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        builder.Property(entity => entity.Name).HasMaxLength(CemeteryPlace.NameMaxLength);
        builder.Property(entity => entity.PlanImageUrl).HasMaxLength(CemeteryPlace.PlanUrlMaxLength);
        builder.Property(entity => entity.PlanBounds).HasConversion(GeometryMapping.PolygonConverter).HasColumnType("geometry(Polygon,4326)");
        builder.Property(entity => entity.RestPeriodYears).HasDefaultValue(RestPeriod.DefaultYears);
        builder.HasIndex(entity => entity.TenantId);
    }
}

public sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("sections");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        builder.Property(entity => entity.Name).HasMaxLength(Section.NameMaxLength);
        builder.Property(entity => entity.Code).HasMaxLength(Section.CodeMaxLength);
        builder.Property(entity => entity.Outline).HasConversion(GeometryMapping.PolygonConverter).HasColumnType("geometry(Polygon,4326)");
        builder.HasIndex(entity => new { entity.TenantId, entity.CemeteryId, entity.Code }).IsUnique().HasDatabaseName("sections_tenant_code_key");
        builder.HasOne<CemeteryPlace>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.CemeteryId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GraveRowConfiguration : IEntityTypeConfiguration<GraveRow>
{
    public void Configure(EntityTypeBuilder<GraveRow> builder)
    {
        builder.ToTable("grave_rows");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        builder.Property(entity => entity.Label).HasMaxLength(GraveRow.LabelMaxLength);
        builder.HasIndex(entity => new { entity.TenantId, entity.SectionId, entity.Label }).IsUnique().HasDatabaseName("grave_rows_tenant_label_key");
        builder.HasOne<Section>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.SectionId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GraveSiteConfiguration : IEntityTypeConfiguration<GraveSite>
{
    public void Configure(EntityTypeBuilder<GraveSite> builder)
    {
        builder.ToTable("grave_sites");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        builder.Property(entity => entity.Code).HasMaxLength(GraveSite.CodeMaxLength);
        builder.Property(entity => entity.Kind)
            .HasConversion(kind => GraveSiteKinds.ToCode(kind), code => ParseKind(code))
            .HasMaxLength(32);
        builder.Property(entity => entity.Outline).HasConversion(GeometryMapping.PolygonConverter).HasColumnType("geometry(Polygon,4326)");
        builder.Property(entity => entity.HiddenFromPublic).HasDefaultValue(false);
        builder.Ignore(entity => entity.Status);
        builder.HasIndex(entity => entity.Outline).HasMethod("gist");
        builder.HasIndex(entity => new { entity.TenantId, entity.CemeteryId, entity.Code }).IsUnique().HasDatabaseName("grave_sites_tenant_code_key");
        builder.HasOne<Section>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.SectionId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CemeteryPlace>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.CemeteryId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GraveRow>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.RowId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static GraveSiteKind ParseKind(string code) =>
        GraveSiteKinds.TryParse(code, out var kind) ? kind : throw new Domain.Identity.DomainRuleException("grave_site.kind_invalid");
}

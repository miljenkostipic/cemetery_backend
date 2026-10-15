using Cemetery.Domain.Layout;
using Cemetery.Domain.Operations;
using Cemetery.Domain.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Infrastructure.Persistence;

public sealed class FuneralConfiguration : IEntityTypeConfiguration<Funeral>
{
    public void Configure(EntityTypeBuilder<Funeral> builder)
    {
        builder.ToTable("funerals");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        builder.Property(entity => entity.Place).HasConversion(place => FuneralPlaces.ToCode(place), code => FuneralPlaces.Parse(code)).HasMaxLength(16);
        builder.Property(entity => entity.Status).HasConversion(status => FuneralStatuses.ToCode(status), code => FuneralStatuses.Parse(code)).HasMaxLength(16);
        builder.Property(entity => entity.StartsAt).HasColumnType("timestamp without time zone");
        builder.Property(entity => entity.EndsAt).HasColumnType("timestamp without time zone");
        builder.HasIndex(entity => new { entity.TenantId, entity.CemeteryId, entity.StartsAt });
        Relate(builder);
    }

    private static void Relate(EntityTypeBuilder<Funeral> builder)
    {
        builder.HasOne<CemeteryPlace>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.CemeteryId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Deceased>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.DeceasedId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GraveSite>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.GraveSiteId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ChapelBookingConfiguration : IEntityTypeConfiguration<ChapelBooking>
{
    public void Configure(EntityTypeBuilder<ChapelBooking> builder)
    {
        builder.ToTable("chapel_bookings");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Place).HasConversion(place => ChapelPlaces.ToCode(place), code => ChapelPlaces.Parse(code)).HasMaxLength(16);
        builder.Property(entity => entity.StartsAt).HasColumnType("timestamp without time zone");
        builder.Property(entity => entity.EndsAt).HasColumnType("timestamp without time zone");
        builder.HasIndex(entity => new { entity.TenantId, entity.CemeteryId, entity.StartsAt });
        builder.HasOne<CemeteryPlace>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.CemeteryId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MonumentPermitConfiguration : IEntityTypeConfiguration<MonumentPermit>
{
    public void Configure(EntityTypeBuilder<MonumentPermit> builder)
    {
        builder.ToTable("monument_permits");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        builder.Property(entity => entity.HolderName).HasMaxLength(OperationsText.NameMaxLength);
        builder.HasIndex(entity => new { entity.TenantId, entity.CemeteryId, entity.GraveSiteId });
        builder.HasOne<CemeteryPlace>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.CemeteryId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GraveSite>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.GraveSiteId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrder>
{
    public void Configure(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.ToTable("work_orders");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        Columns(builder);
        Relate(builder);
    }

    private static void Columns(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.Property(entity => entity.Kind).HasConversion(kind => WorkKinds.ToCode(kind), code => WorkKinds.Parse(code)).HasMaxLength(16);
        builder.Property(entity => entity.Status).HasConversion(status => WorkOrderStatuses.ToCode(status), code => WorkOrderStatuses.Parse(code)).HasMaxLength(16);
        builder.Property(entity => entity.Notes).HasMaxLength(OperationsText.NoteMaxLength);
        builder.Property(entity => entity.CompletionNote).HasMaxLength(OperationsText.NoteMaxLength);
        builder.HasIndex(entity => new { entity.TenantId, entity.ScheduledOn, entity.Status });
    }

    private static void Relate(EntityTypeBuilder<WorkOrder> builder)
    {
        builder.HasOne<CemeteryPlace>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.CemeteryId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GraveSite>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.GraveSiteId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MonumentPermit>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.PermitId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkOrderPhotoConfiguration : IEntityTypeConfiguration<WorkOrderPhoto>
{
    public void Configure(EntityTypeBuilder<WorkOrderPhoto> builder)
    {
        builder.ToTable("work_order_photos");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.ContentType).HasMaxLength(32);
        builder.Property(entity => entity.Content).HasColumnType("bytea");
        builder.HasIndex(entity => new { entity.TenantId, entity.WorkOrderId });
        builder.HasOne<WorkOrder>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.WorkOrderId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

using Cemetery.Domain.Layout;
using Cemetery.Domain.Rights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Infrastructure.Persistence;

public sealed class HolderConfiguration : IEntityTypeConfiguration<Holder>
{
    public void Configure(EntityTypeBuilder<Holder> builder)
    {
        builder.ToTable("holders");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        builder.Property(entity => entity.GivenName).HasMaxLength(PersonNameMax);
        builder.Property(entity => entity.FamilyName).HasMaxLength(PersonNameMax);
        builder.Property(entity => entity.Oib).HasMaxLength(OibNumber.Length);
        builder.Property(entity => entity.Address).HasMaxLength(Holder.AddressMaxLength);
        builder.Property(entity => entity.Email).HasMaxLength(256);
        builder.Property(entity => entity.Phone).HasMaxLength(Holder.PhoneMaxLength);
        builder.HasIndex(entity => new { entity.TenantId, entity.Email }).IsUnique().HasFilter("email IS NOT NULL").HasDatabaseName("holders_email_key");
        builder.HasIndex(entity => new { entity.TenantId, entity.FamilyName, entity.GivenName, entity.Id });
    }

    private const int PersonNameMax = 80;
}

public sealed class GraveRightConfiguration : IEntityTypeConfiguration<GraveRight>
{
    public void Configure(EntityTypeBuilder<GraveRight> builder)
    {
        builder.ToTable("grave_rights");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasConversion<string>(new SnakeCaseEnumConverter<GraveRightStatus>()).HasMaxLength(16);
        builder.HasIndex(entity => new { entity.TenantId, entity.GraveSiteId }).IsUnique().HasFilter("status = 'active'").HasDatabaseName("grave_rights_one_active");
        builder.HasOne<GraveSite>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.GraveSiteId }).HasPrincipalKey(site => new { site.TenantId, site.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(entity => entity.Shares, share =>
        {
            share.ToTable("grave_right_shares");
            share.WithOwner().HasForeignKey("GraveRightId");
            share.Property<Guid>("Id");
            share.HasKey("Id");
            share.Property(item => item.HolderId);
            share.Property(item => item.Percent);
            share.Property(item => item.TenantId);
            share.HasIndex("GraveRightId", nameof(RightShare.HolderId));
        });
    }
}

public sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("reservations");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => new { entity.TenantId, entity.GraveSiteId }).IsUnique().HasFilter("active").HasDatabaseName("reservations_one_active");
        builder.HasOne<GraveSite>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.GraveSiteId }).HasPrincipalKey(site => new { site.TenantId, site.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Holder>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.HolderId }).HasPrincipalKey(holder => new { holder.TenantId, holder.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SiteDocumentConfiguration : IEntityTypeConfiguration<SiteDocument>
{
    public void Configure(EntityTypeBuilder<SiteDocument> builder)
    {
        builder.ToTable("site_documents");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Kind).HasConversion<string>(new SnakeCaseEnumConverter<DocumentKind>()).HasMaxLength(16);
        builder.Property(entity => entity.FileName).HasMaxLength(DocumentBytes.NameMaxLength);
        builder.Property(entity => entity.ContentType).HasMaxLength(64);
        builder.Property(entity => entity.StorageKey).HasMaxLength(StorageKeys.MaxLength);
        builder.HasOne<GraveSite>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.GraveSiteId }).HasPrincipalKey(site => new { site.TenantId, site.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("stored_files");
        builder.HasKey(entity => entity.Id);
    }
}

public sealed class EntranceConfiguration : IEntityTypeConfiguration<Entrance>
{
    public void Configure(EntityTypeBuilder<Entrance> builder)
    {
        builder.ToTable("entrances");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(Entrance.NameMaxLength);
        builder.Property(entity => entity.Location).HasConversion(GeometryMapping.PointConverter).HasColumnType("geometry(Point,4326)");
        builder.Property(entity => entity.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasIndex(entity => entity.Location).HasMethod("gist");
        builder.HasOne<CemeteryPlace>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.CemeteryId }).HasPrincipalKey(place => new { place.TenantId, place.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CemeteryPathConfiguration : IEntityTypeConfiguration<CemeteryPath>
{
    public void Configure(EntityTypeBuilder<CemeteryPath> builder)
    {
        builder.ToTable("paths");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Name).HasMaxLength(CemeteryPath.NameMaxLength);
        builder.Property(entity => entity.Line).HasConversion(GeometryMapping.LineConverter).HasColumnType("geometry(LineString,4326)");
        builder.Property(entity => entity.UpdatedAt).HasDefaultValueSql("now()");
        builder.HasOne<CemeteryPlace>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.CemeteryId }).HasPrincipalKey(place => new { place.TenantId, place.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

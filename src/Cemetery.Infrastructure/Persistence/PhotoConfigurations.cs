using Cemetery.Domain.Layout;
using Cemetery.Domain.Photos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cemetery.Infrastructure.Persistence;

public sealed class GraveSitePhotoConfiguration : IEntityTypeConfiguration<GraveSitePhoto>
{
    public void Configure(EntityTypeBuilder<GraveSitePhoto> builder)
    {
        builder.ToTable("grave_site_photos");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.FullFileId).HasMaxLength(StorageKeyMax);
        builder.Property(entity => entity.ThumbFileId).HasMaxLength(StorageKeyMax);
        builder.Property(entity => entity.Caption).HasMaxLength(GraveSitePhotoDetails.CaptionMaxLength);
        builder.HasIndex(entity => new { entity.TenantId, entity.GraveSiteId });
        builder.HasOne<GraveSite>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.GraveSiteId }).HasPrincipalKey(site => new { site.TenantId, site.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private const int StorageKeyMax = 64;
}

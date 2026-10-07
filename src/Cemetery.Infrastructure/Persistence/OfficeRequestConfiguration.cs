using Cemetery.Domain.Layout;
using Cemetery.Domain.Rights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cemetery.Infrastructure.Persistence;

public sealed class OfficeRequestConfiguration : IEntityTypeConfiguration<OfficeRequest>
{
    public void Configure(EntityTypeBuilder<OfficeRequest> builder)
    {
        builder.ToTable("office_requests");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Message).HasMaxLength(OfficeRequest.MaxMessageLength);
        builder.HasIndex(entity => new { entity.TenantId, entity.CreatedOn });
        builder.HasOne<GraveSite>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.GraveSiteId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Cemetery.Domain.Launch;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cemetery.Infrastructure.Persistence;

public sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("webhook_subscriptions");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        builder.Property(entity => entity.Url).HasMaxLength(WebhookAddress.MaxLength);
        builder.Property(entity => entity.Event).HasMaxLength(64);
        builder.Property(entity => entity.Secret).HasMaxLength(WebhookSubscription.SecretMaxLength);
        builder.HasIndex(entity => new { entity.TenantId, entity.Event });
    }
}

public sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("webhook_deliveries");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Event).HasMaxLength(64);
        builder.Property(entity => entity.Status).HasMaxLength(16);
        builder.HasIndex(entity => new { entity.TenantId, entity.SubscriptionId });
        builder.HasOne<WebhookSubscription>()
            .WithMany()
            .HasForeignKey(entity => new { entity.TenantId, entity.SubscriptionId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MaticaLinkConfiguration : IEntityTypeConfiguration<MaticaLink>
{
    public void Configure(EntityTypeBuilder<MaticaLink> builder)
    {
        builder.ToTable("matica_links");
        builder.HasKey(entity => entity.Id);
        builder.HasIndex(entity => new { entity.TenantId, entity.IntermentId }).IsUnique().HasDatabaseName("matica_links_interment_key");
        builder.HasIndex(entity => new { entity.TenantId, entity.DeathCertificateId }).IsUnique().HasDatabaseName("matica_links_certificate_key");
        builder.HasIndex(entity => new { entity.TenantId, entity.CemeteryId });
    }
}

public sealed class GdprExportConfiguration : IEntityTypeConfiguration<GdprExport>
{
    public void Configure(EntityTypeBuilder<GdprExport> builder)
    {
        builder.ToTable("gdpr_exports");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Status).HasMaxLength(16);
        builder.HasIndex(entity => new { entity.TenantId, entity.RequestedAt });
    }
}

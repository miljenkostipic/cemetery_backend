using Cemetery.Domain.Billing;
using Cemetery.Domain.Layout;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Infrastructure.Persistence;

public sealed class FeeScheduleConfiguration : IEntityTypeConfiguration<FeeSchedule>
{
    public void Configure(EntityTypeBuilder<FeeSchedule> builder)
    {
        builder.ToTable("fee_schedules");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        Columns(builder);
        builder.HasIndex(entity => new { entity.TenantId, entity.CemeteryId });
        builder.HasOne<CemeteryPlace>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.CemeteryId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private static void Columns(EntityTypeBuilder<FeeSchedule> builder)
    {
        builder.Property(entity => entity.Kind).HasMaxLength(FeeKinds.MaxLength);
        builder.Property(entity => entity.Name).HasMaxLength(FeeName.MaxLength);
        builder.Property(entity => entity.Amount).HasPrecision(FeeAmount.Precision, FeeAmount.Scale);
        builder.Property(entity => entity.Currency).HasMaxLength(FeeCurrencies.MaxLength);
    }
}

public sealed class ChargeConfiguration : IEntityTypeConfiguration<Charge>
{
    public void Configure(EntityTypeBuilder<Charge> builder)
    {
        builder.ToTable("charges");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        Columns(builder);
        builder.HasIndex(entity => new { entity.TenantId, entity.CemeteryId, entity.GraveSiteId, entity.FeeScheduleId, entity.Year })
            .IsUnique()
            .HasDatabaseName("charges_year_key");
        Relate(builder);
    }

    private static void Columns(EntityTypeBuilder<Charge> builder)
    {
        builder.Property(entity => entity.GraveSiteCode).HasMaxLength(GraveSite.CodeMaxLength);
        builder.Property(entity => entity.HolderName).HasMaxLength(Charge.HolderMaxLength);
        builder.Property(entity => entity.Kind).HasMaxLength(FeeKinds.MaxLength);
        builder.Property(entity => entity.Description).HasMaxLength(FeeName.MaxLength);
        builder.Property(entity => entity.Amount).HasPrecision(FeeAmount.Precision, FeeAmount.Scale);
        builder.Property(entity => entity.Currency).HasMaxLength(FeeCurrencies.MaxLength);
        builder.Property(entity => entity.Status).HasMaxLength(ChargeStatuses.MaxLength);
    }

    private static void Relate(EntityTypeBuilder<Charge> builder)
    {
        builder.HasOne<CemeteryPlace>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.CemeteryId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GraveSite>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.GraveSiteId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FeeSchedule>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.FeeScheduleId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MerchantAccountConfiguration : IEntityTypeConfiguration<MerchantAccount>
{
    public void Configure(EntityTypeBuilder<MerchantAccount> builder)
    {
        builder.ToTable("merchant_accounts");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Provider).HasMaxLength(BillingProviders.MaxLength);
        builder.Property(entity => entity.MerchantId).HasMaxLength(MerchantCode.MaxLength);
        builder.Property(entity => entity.Secret).HasMaxLength(ProviderSecret.MaxLength);
        builder.HasIndex(entity => new { entity.TenantId, entity.Provider }).IsUnique().HasDatabaseName("merchant_accounts_provider_key");
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(entity => entity.Id);
        builder.HasAlternateKey(entity => new { entity.TenantId, entity.Id });
        Columns(builder);
        builder.HasIndex(entity => new { entity.TenantId, entity.ChargeId });
        builder.HasOne<Charge>().WithMany().HasForeignKey(entity => new { entity.TenantId, entity.ChargeId })
            .HasPrincipalKey(entity => new { entity.TenantId, entity.Id }).OnDelete(DeleteBehavior.Restrict);
    }

    private static void Columns(EntityTypeBuilder<Payment> builder)
    {
        builder.Property(entity => entity.Method).HasMaxLength(ManualMethods.MaxLength);
        builder.Property(entity => entity.Amount).HasPrecision(FeeAmount.Precision, FeeAmount.Scale);
        builder.Property(entity => entity.Currency).HasMaxLength(FeeCurrencies.MaxLength);
        builder.Property(entity => entity.Status).HasMaxLength(PaymentStatuses.MaxLength);
        builder.Property(entity => entity.Reference).HasMaxLength(PaymentReference.MaxLength);
        builder.Property(entity => entity.ReceiptNumber).HasMaxLength(Payment.ReceiptMaxLength);
        builder.Property(entity => entity.Description).HasMaxLength(Payment.DescriptionMaxLength);
        builder.Property(entity => entity.VoidReason).HasMaxLength(BillingReason.MaxLength);
    }
}

using Cemetery.Domain.Billing;
using Cemetery.Domain.Integrations;
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
        builder.Property(entity => entity.Kind).HasConversion<string>(new SnakeCaseEnumConverter<FeeKind>()).HasMaxLength(FeeKinds.MaxLength);
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
        builder.Property(entity => entity.HolderId);
        builder.HasIndex(entity => new { entity.TenantId, entity.CemeteryId, entity.GraveSiteId, entity.FeeScheduleId, entity.Year })
            .IsUnique()
            .HasFilter("holder_id IS NULL")
            .HasDatabaseName("charges_year_key");
        builder.HasIndex(entity => new { entity.TenantId, entity.CemeteryId, entity.GraveSiteId, entity.FeeScheduleId, entity.Year, entity.HolderId })
            .IsUnique()
            .HasFilter("holder_id IS NOT NULL")
            .HasDatabaseName("charges_holder_year_key");
        Relate(builder);
    }

    private static void Columns(EntityTypeBuilder<Charge> builder)
    {
        builder.Property(entity => entity.GraveSiteCode).HasMaxLength(GraveSite.CodeMaxLength);
        builder.Property(entity => entity.HolderName).HasMaxLength(Charge.HolderMaxLength);
        builder.Property(entity => entity.Kind).HasConversion<string>(new SnakeCaseEnumConverter<FeeKind>()).HasMaxLength(FeeKinds.MaxLength);
        builder.Property(entity => entity.Description).HasMaxLength(FeeName.MaxLength);
        builder.Property(entity => entity.Amount).HasPrecision(FeeAmount.Precision, FeeAmount.Scale);
        builder.Property(entity => entity.Currency).HasMaxLength(FeeCurrencies.MaxLength);
        builder.Property(entity => entity.Status).HasConversion<string>(new SnakeCaseEnumConverter<ChargeStatus>()).HasMaxLength(ChargeStatuses.MaxLength);
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
        builder.Property(entity => entity.Provider).HasConversion<string>(new SnakeCaseEnumConverter<BillingProvider>()).HasMaxLength(BillingProviders.MaxLength);
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
        builder.Property(entity => entity.Method).HasConversion<string>(new SnakeCaseEnumConverter<ManualMethod>()).HasMaxLength(ManualMethods.MaxLength);
        builder.Property(entity => entity.Amount).HasPrecision(FeeAmount.Precision, FeeAmount.Scale);
        builder.Property(entity => entity.Currency).HasMaxLength(FeeCurrencies.MaxLength);
        builder.Property(entity => entity.Status).HasConversion<string>(new SnakeCaseEnumConverter<PaymentStatus>()).HasMaxLength(PaymentStatuses.MaxLength);
        builder.Property(entity => entity.Reference).HasMaxLength(PaymentReference.MaxLength);
        builder.Property(entity => entity.ReceiptNumber).HasMaxLength(Payment.ReceiptMaxLength);
        builder.Property(entity => entity.Description).HasMaxLength(Payment.DescriptionMaxLength);
        builder.Property(entity => entity.VoidReason).HasMaxLength(BillingReason.MaxLength);
        builder.Property(entity => entity.ProviderTransactionId).HasMaxLength(ProviderTransaction.MaxLength);
        builder.HasIndex(entity => new { entity.TenantId, entity.ProviderTransactionId })
            .IsUnique()
            .HasFilter("provider_transaction_id IS NOT NULL")
            .HasDatabaseName("payments_provider_transaction_key");
    }
}

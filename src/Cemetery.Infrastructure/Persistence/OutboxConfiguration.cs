using Cemetery.Domain.Integrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cemetery.Infrastructure.Persistence;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Type).HasMaxLength(OutboxMessage.TypeMaxLength);
        builder.Property(entity => entity.Payload).HasColumnType("jsonb");
        builder.Property(entity => entity.LastError).HasMaxLength(OutboxMessage.ErrorMaxLength);
        builder.HasIndex(entity => new { entity.ProcessedAt, entity.OccurredAt });
    }
}

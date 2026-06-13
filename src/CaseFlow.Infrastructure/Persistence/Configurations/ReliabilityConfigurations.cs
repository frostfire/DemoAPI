using CaseFlow.Domain.Cases;
using CaseFlow.Infrastructure.Idempotency;
using CaseFlow.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CaseFlow.Infrastructure.Persistence.Configurations;

public sealed class CaseAuditEntryConfiguration : IEntityTypeConfiguration<CaseAuditEntry>
{
    public void Configure(EntityTypeBuilder<CaseAuditEntry> builder)
    {
        builder.ToTable("CaseAuditLog");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.OrganizationId).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Action).HasMaxLength(64).IsRequired();
        builder.Property(a => a.PerformedByUserId).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Details).HasMaxLength(1000);

        // The only read path is "history of one case", newest first.
        builder.HasIndex(a => new { a.CaseId, a.OccurredAt });
    }
}

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Type).HasMaxLength(128).IsRequired();
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(500);

        // The worker polls for unprocessed messages; a partial index keeps
        // that scan cheap no matter how large the processed backlog grows.
        builder.HasIndex(m => m.OccurredAt).HasFilter("\"ProcessedAt\" IS NULL");
    }
}

public sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(r => r.Key);

        builder.Property(r => r.Key).HasMaxLength(128);
        builder.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(r => r.ResponseBody).IsRequired();

        // Phase 5 adds the scheduled cleanup that prunes expired records.
        builder.HasIndex(r => r.ExpiresAt);
    }
}

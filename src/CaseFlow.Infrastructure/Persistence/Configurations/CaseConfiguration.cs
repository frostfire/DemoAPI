using CaseFlow.Domain.Cases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CaseFlow.Infrastructure.Persistence.Configurations;

public sealed class CaseConfiguration : IEntityTypeConfiguration<Case>
{
    public void Configure(EntityTypeBuilder<Case> builder)
    {
        builder.ToTable("Cases");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.OrganizationId).HasMaxLength(64).IsRequired();
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(4000);
        builder.Property(c => c.RejectReason).HasMaxLength(1000);
        builder.Property(c => c.CreatedByUserId).HasMaxLength(64).IsRequired();
        builder.Property(c => c.SubmittedByUserId).HasMaxLength(64);
        builder.Property(c => c.ReviewedByUserId).HasMaxLength(64);

        // Enums stored as text. Costs a few bytes per row, saves every future
        // reader of the database from decoding magic integers - and the values
        // never get silently re-numbered by an enum reorder.
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.Priority).HasConversion<string>().HasMaxLength(32).IsRequired();

        // Postgres bumps xmin on every UPDATE; mapping it as the row version
        // gives optimistic concurrency without an extra column or trigger.
        builder.Property(c => c.Version).IsRowVersion();

        builder.Ignore(c => c.DomainEvents);

        // The search query filters on these. CreatedAt is the default sort.
        builder.HasIndex(c => c.OrganizationId);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.CreatedAt);
    }
}

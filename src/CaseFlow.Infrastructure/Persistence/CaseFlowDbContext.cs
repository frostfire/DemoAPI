using CaseFlow.Domain.Cases;
using CaseFlow.Infrastructure.Idempotency;
using CaseFlow.Infrastructure.Outbox;
using Microsoft.EntityFrameworkCore;

namespace CaseFlow.Infrastructure.Persistence;

public class CaseFlowDbContext(DbContextOptions<CaseFlowDbContext> options) : DbContext(options)
{
    public DbSet<Case> Cases => Set<Case>();
    public DbSet<CaseAuditEntry> CaseAuditLog => Set<CaseAuditEntry>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CaseFlowDbContext).Assembly);
    }

    // This override is the outbox pattern. Any domain events raised by
    // tracked cases become audit entries and outbox messages in the same
    // SaveChanges - one transaction covers the state change, its audit
    // trail, and the message that will drive notifications. Either all of
    // it commits or none of it does.
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var casesWithEvents = ChangeTracker.Entries<Case>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var @case in casesWithEvents)
        {
            foreach (var domainEvent in @case.DomainEvents)
            {
                CaseAuditLog.Add(CaseAuditEntry.FromEvent(domainEvent));
                OutboxMessages.Add(OutboxMessage.FromEvent(domainEvent));
            }

            @case.ClearDomainEvents();
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

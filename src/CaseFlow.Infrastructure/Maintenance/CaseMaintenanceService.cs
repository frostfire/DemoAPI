using CaseFlow.Domain.Cases;
using CaseFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CaseFlow.Infrastructure.Maintenance;

// The work behind the Quartz cron jobs. Bulk set-based deletes via
// ExecuteDeleteAsync - no entities loaded, no change tracking, one round trip
// each. Kept separate from the job classes so the jobs stay trivial.
public sealed class CaseMaintenanceService(CaseFlowDbContext dbContext, TimeProvider clock)
{
    public async Task<int> ExpireStaleDraftsAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow() - olderThan;

        // Abandoned drafts only. Anything that entered review keeps its audit
        // history; the CaseCreated audit row for an expired draft is left in
        // place deliberately - the log outlives the entity.
        return await dbContext.Cases
            .Where(c => c.Status == CaseStatus.Draft && c.UpdatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteExpiredIdempotencyRecordsAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();

        return await dbContext.IdempotencyRecords
            .Where(r => r.ExpiresAt < now)
            .ExecuteDeleteAsync(cancellationToken);
    }
}

using CaseFlow.Infrastructure.Maintenance;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CaseFlow.Infrastructure.Scheduling.Quartz;

// Quartz owns the time-driven jobs: their identity is a schedule. Each is
// marked [DisallowConcurrentExecution] so a slow run can never overlap its
// next trigger. The thresholds are constants here for readability; a real
// deployment would bind them from configuration.

[DisallowConcurrentExecution]
public sealed class ExpireStaleDraftsJob(
    CaseMaintenanceService maintenance,
    ILogger<ExpireStaleDraftsJob> logger) : IJob
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromDays(30);

    public async Task Execute(IJobExecutionContext context)
    {
        var removed = await maintenance.ExpireStaleDraftsAsync(StaleAfter, context.CancellationToken);
        if (removed > 0)
        {
            logger.LogInformation("Stale draft expiration removed {Count} abandoned draft(s)", removed);
        }
    }
}

[DisallowConcurrentExecution]
public sealed class CleanupIdempotencyRecordsJob(
    CaseMaintenanceService maintenance,
    ILogger<CleanupIdempotencyRecordsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var removed = await maintenance.DeleteExpiredIdempotencyRecordsAsync(context.CancellationToken);
        if (removed > 0)
        {
            logger.LogInformation("Idempotency cleanup removed {Count} expired record(s)", removed);
        }
    }
}

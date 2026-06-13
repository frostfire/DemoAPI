using CaseFlow.Infrastructure.Demo;
using Microsoft.Extensions.Logging;
using Quartz;

namespace CaseFlow.Infrastructure.Scheduling.Quartz;

// Resets the public sandbox to seed state on a schedule (registered only in
// demo mode). Quartz owns it because its identity is a cadence, same as the
// other maintenance jobs.
[DisallowConcurrentExecution]
public sealed class DemoDataResetJob(
    DemoDataSeeder seeder,
    ILogger<DemoDataResetJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation("Running scheduled demo data reset");
        await seeder.ResetAsync(context.CancellationToken);
    }
}

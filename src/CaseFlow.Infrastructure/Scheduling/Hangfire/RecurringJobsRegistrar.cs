using global::Hangfire;
using Microsoft.Extensions.Hosting;

namespace CaseFlow.Infrastructure.Scheduling.Hangfire;

// Registers Hangfire recurring jobs on worker startup. Kept as a hosted
// service rather than inline so it runs once the storage is ready and is easy
// to find alongside the server registration.
public sealed class RecurringJobsRegistrar(IRecurringJobManager recurringJobs) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Every minute. Contrast with the Quartz jobs: Hangfire identifies
        // this by job id ("outbox-dispatch"), records every run in the
        // dashboard, and owns its retry policy. Quartz identifies its work by
        // the trigger schedule instead.
        recurringJobs.AddOrUpdate<OutboxDispatchJob>(
            "outbox-dispatch",
            job => job.RunAsync(),
            Cron.Minutely);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

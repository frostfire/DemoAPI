using CaseFlow.Infrastructure.Outbox;
using global::Hangfire;

namespace CaseFlow.Infrastructure.Scheduling.Hangfire;

// The recurring drain of the outbox, registered as a Hangfire recurring job
// (see RecurringJobsRegistrar). This replaces the hand-rolled BackgroundService
// poll loop from phase 4: same OutboxBatchProcessor, but now every run shows
// up in the dashboard and Hangfire handles the scheduling and retry policy.
//
// [DisableConcurrentExecution] is Hangfire's guard - if a drain runs long, the
// next minute's trigger waits rather than double-processing.
public sealed class OutboxDispatchJob(OutboxBatchProcessor processor)
{
    [DisableConcurrentExecution(timeoutInSeconds: 55)]
    public async Task RunAsync()
    {
        // Drain fully each run - a backlog should not wait a whole minute per
        // batch for the next trigger.
        while (await processor.ProcessBatchAsync() > 0)
        {
        }
    }
}

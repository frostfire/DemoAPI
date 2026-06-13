using CaseFlow.Application.Abstractions;
using global::Hangfire;

namespace CaseFlow.Infrastructure.Scheduling.Hangfire;

// The IJobScheduler implementation. Enqueuing happens wherever the API runs;
// the job itself executes in the worker's Hangfire server. Both processes
// point at the same Hangfire storage, so the schedule survives a restart of
// either side.
public sealed class HangfireJobScheduler(IBackgroundJobClient backgroundJobs) : IJobScheduler
{
    public void ScheduleCaseAutoArchive(Guid caseId, TimeSpan delay)
        => backgroundJobs.Schedule<CaseAutoArchiveJob>(job => job.RunAsync(caseId), delay);
}

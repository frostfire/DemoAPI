namespace CaseFlow.Application.Abstractions;

// Lets the application schedule deferred work without knowing what runs it.
// The Hangfire implementation lives in Infrastructure; the application layer
// stays free of any scheduler dependency, which is also what keeps the
// architecture tests happy.
public interface IJobScheduler
{
    // Schedules the approved case to be archived after the given delay.
    void ScheduleCaseAutoArchive(Guid caseId, TimeSpan delay);
}

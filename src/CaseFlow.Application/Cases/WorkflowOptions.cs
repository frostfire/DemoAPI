namespace CaseFlow.Application.Cases;

public sealed class WorkflowOptions
{
    public const string SectionName = "Workflow";

    // How long an approved case waits before it auto-archives. Long by
    // default; the hosted demo sets it short so the scheduled Hangfire job is
    // visibly pending in the dashboard.
    public TimeSpan AutoArchiveAfter { get; init; } = TimeSpan.FromDays(30);
}

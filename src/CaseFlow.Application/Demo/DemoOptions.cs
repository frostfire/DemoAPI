namespace CaseFlow.Application.Demo;

// Controls the public hosted sandbox. Off by default and everywhere that might
// run next to real data; turned on only for the demo deployment. When on, the
// app seeds sample data, exposes Swagger UI and a read-only jobs dashboard,
// caps how much a visitor can create, and resets itself on a schedule.
public sealed class DemoOptions
{
    public const string SectionName = "Demo";

    public bool Enabled { get; init; }

    // Per-organization ceiling so a visitor cannot flood the throwaway
    // database between resets. 0 means unlimited.
    public int MaxCasesPerOrganization { get; init; } = 50;

    // How often the sandbox is wiped back to seed state.
    public TimeSpan ResetEvery { get; init; } = TimeSpan.FromHours(3);
}

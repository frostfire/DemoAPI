using global::Hangfire.Dashboard;

namespace CaseFlow.Infrastructure.Scheduling.Hangfire;

// Hangfire blocks remote dashboard access by default. This filter makes the
// policy explicit and configurable: open in local development, closed
// elsewhere for now. Phase 7 (the public demo) relaxes this to a read-only
// dashboard behind the demo-mode flag.
public sealed class HangfireDashboardAuthorizationFilter(bool allowAccess) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context) => allowAccess;
}

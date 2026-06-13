using CaseFlow.Application.Abstractions;

namespace CaseFlow.Worker;

// Background jobs have no logged-in caller - they act as the system. This is
// the worker's ICurrentUser, satisfying the application handlers the same way
// the API's HttpContext-backed CurrentUser does on the request side.
//
// The auto-archive handler records "system" as the actor itself and does not
// read this, but registering a real principal keeps the full handler set
// composable inside the worker (and DI validation honest).
internal sealed class SystemCurrentUser : ICurrentUser
{
    public string UserId => "system";
    public string OrganizationId => "system";
    public bool IsAdmin => true;
}

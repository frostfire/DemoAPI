using System.Security.Claims;
using CaseFlow.Application.Abstractions;

namespace CaseFlow.Api.Auth;

// Bridges HttpContext claims to the application's ICurrentUser. Endpoints
// that reach handlers are all behind [Authorize], so missing claims here
// mean a misconfigured token source, not an anonymous caller - fail loudly.
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("No HTTP context available.");

    public string UserId =>
        Principal.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Token is missing the 'sub' claim.");

    public string OrganizationId =>
        Principal.FindFirstValue("org")
        ?? throw new InvalidOperationException("Token is missing the 'org' claim.");

    public bool IsAdmin => Principal.IsInRole(CaseRoles.Admin);
}

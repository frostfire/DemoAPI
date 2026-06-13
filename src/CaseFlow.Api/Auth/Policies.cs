using Microsoft.AspNetCore.Authorization;

namespace CaseFlow.Api.Auth;

// Role policies answer "may this caller attempt this kind of action".
// Whether they may touch a specific case is object-level authorization and
// lives in the application layer (CaseAccess). Admin satisfies everything.
public static class Policies
{
    public const string CanReadCases = nameof(CanReadCases);
    public const string CanWriteCases = nameof(CanWriteCases);
    public const string CanReviewCases = nameof(CanReviewCases);
    public const string CanApproveCases = nameof(CanApproveCases);
    public const string AdminOnly = nameof(AdminOnly);

    public static void AddCaseFlowPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(CanReadCases, p => p.RequireRole(CaseRoles.All));
        options.AddPolicy(CanWriteCases, p => p.RequireRole(CaseRoles.Writer, CaseRoles.Admin));
        options.AddPolicy(CanReviewCases, p => p.RequireRole(CaseRoles.Reviewer, CaseRoles.Approver, CaseRoles.Admin));
        options.AddPolicy(CanApproveCases, p => p.RequireRole(CaseRoles.Approver, CaseRoles.Admin));
        options.AddPolicy(AdminOnly, p => p.RequireRole(CaseRoles.Admin));
    }
}

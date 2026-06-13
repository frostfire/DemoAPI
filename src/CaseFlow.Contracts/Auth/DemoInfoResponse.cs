namespace CaseFlow.Contracts.Auth;

// Transparency for sandbox visitors: what mode they're in, the limits, and how
// to get a token. Returned by GET /api/v1/demo/info.
public sealed record DemoInfoResponse(
    bool DemoMode,
    int MaxCasesPerOrganization,
    string ResetEvery,
    IReadOnlyList<string> AvailableRoles,
    string HowToAuthenticate);

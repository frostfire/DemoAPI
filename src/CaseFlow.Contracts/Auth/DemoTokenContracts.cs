namespace CaseFlow.Contracts.Auth;

// Request a short-lived demo token with a chosen set of roles. This exists so
// anyone exploring the API (locally or on the hosted sandbox) can exercise
// the authorization rules without a registration flow. Multiple roles are
// allowed so a single user can act as, say, Writer and Approver - which is
// exactly what the self-approval rule needs to demonstrate itself.
public sealed record DemoTokenRequest(
    IReadOnlyList<string> Roles,
    string? OrganizationId = null,
    string? UserId = null);

public sealed record DemoTokenResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string UserId,
    string OrganizationId,
    IReadOnlyList<string> Roles);

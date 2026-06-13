namespace CaseFlow.Contracts.Cases;

// Note there is no organizationId or userId anywhere in these requests - the
// server takes both from the access token. A client that could name its own
// organization would defeat the org isolation rules.
public sealed record CreateCaseRequest(
    string Title,
    string? Description,
    CasePriority Priority);

public sealed record UpdateCaseRequest(
    string Title,
    string? Description,
    CasePriority Priority);

public sealed record RejectCaseRequest(string Reason);

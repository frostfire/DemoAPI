namespace CaseFlow.Contracts.Cases;

public sealed record CaseAuditEntryResponse(
    Guid Id,
    string Action,
    string PerformedByUserId,
    string? Details,
    DateTimeOffset OccurredAt);

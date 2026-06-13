namespace CaseFlow.Contracts.Cases;

public sealed record CaseResponse(
    Guid Id,
    string OrganizationId,
    string Title,
    string? Description,
    CaseStatus Status,
    CasePriority Priority,
    string? RejectReason,
    string CreatedByUserId,
    string? SubmittedByUserId,
    string? ReviewedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt);

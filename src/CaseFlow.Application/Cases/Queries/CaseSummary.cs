using CaseFlow.Domain.Cases;

namespace CaseFlow.Application.Cases.Queries;

// Flat read model returned by the search query. It mirrors the case columns
// directly because the search SQL projects them directly - no entity
// tracking, no lazy anything.
public sealed record CaseSummary(
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

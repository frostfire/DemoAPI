using CaseFlow.Domain.Cases;

namespace CaseFlow.Application.Cases.Commands;

// ExpectedVersion carries the If-Match ETag when the client sent one. Null
// means the client opted out of the concurrency check (last write wins).
public sealed record UpdateCaseCommand(
    Guid CaseId,
    string Title,
    string? Description,
    CasePriority Priority,
    uint? ExpectedVersion = null);

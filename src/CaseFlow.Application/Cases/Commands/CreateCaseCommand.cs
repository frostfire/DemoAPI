using CaseFlow.Domain.Cases;

namespace CaseFlow.Application.Cases.Commands;

// No organization or user fields here: both come from the caller's token via
// ICurrentUser. Clients never get to assert which org they belong to.
public sealed record CreateCaseCommand(
    string Title,
    string? Description,
    CasePriority Priority);

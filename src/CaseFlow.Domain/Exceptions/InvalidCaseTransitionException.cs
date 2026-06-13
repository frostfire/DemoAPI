using CaseFlow.Domain.Cases;

namespace CaseFlow.Domain.Exceptions;

public sealed class InvalidCaseTransitionException(CaseStatus currentStatus, string attemptedAction)
    : DomainException($"A case in the {currentStatus} state cannot be {attemptedAction}.")
{
    public CaseStatus CurrentStatus { get; } = currentStatus;
    public string AttemptedAction { get; } = attemptedAction;
}

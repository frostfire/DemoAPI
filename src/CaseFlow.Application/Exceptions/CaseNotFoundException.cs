namespace CaseFlow.Application.Exceptions;

public sealed class CaseNotFoundException(Guid caseId)
    : Exception($"Case '{caseId}' was not found.")
{
    public Guid CaseId { get; } = caseId;
}

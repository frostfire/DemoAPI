using CaseFlow.Domain.Cases;

namespace CaseFlow.Application.Abstractions;

public interface ICaseAuditQueries
{
    Task<IReadOnlyList<CaseAuditEntry>> GetForCaseAsync(Guid caseId, CancellationToken cancellationToken = default);
}

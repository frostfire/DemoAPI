using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Queries;
using CaseFlow.Application.Exceptions;
using CaseFlow.Domain.Cases;

namespace CaseFlow.Application.Cases.Handlers;

public sealed class GetCaseAuditHandler(
    ICaseRepository repository,
    ICaseAuditQueries auditQueries,
    ICurrentUser currentUser) : IQueryHandler<GetCaseAuditQuery, IReadOnlyList<CaseAuditEntry>>
{
    public async Task<IReadOnlyList<CaseAuditEntry>> HandleAsync(GetCaseAuditQuery query, CancellationToken cancellationToken = default)
    {
        // The audit trail is as sensitive as the case itself - same gate.
        var @case = await repository.GetAsync(query.CaseId, cancellationToken)
            ?? throw new CaseNotFoundException(query.CaseId);
        @case.EnsureAccessibleBy(currentUser);

        return await auditQueries.GetForCaseAsync(query.CaseId, cancellationToken);
    }
}

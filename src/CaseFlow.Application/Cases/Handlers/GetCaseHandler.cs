using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Queries;
using CaseFlow.Application.Exceptions;
using CaseFlow.Domain.Cases;

namespace CaseFlow.Application.Cases.Handlers;

public sealed class GetCaseHandler(ICaseRepository repository, ICurrentUser currentUser)
    : IQueryHandler<GetCaseQuery, Case>
{
    public async Task<Case> HandleAsync(GetCaseQuery query, CancellationToken cancellationToken = default)
    {
        var @case = await repository.GetAsync(query.CaseId, cancellationToken)
            ?? throw new CaseNotFoundException(query.CaseId);

        return @case.EnsureAccessibleBy(currentUser);
    }
}

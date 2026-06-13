using CaseFlow.Application.Cases.Queries;
using CaseFlow.Application.Common;

namespace CaseFlow.Application.Abstractions;

// Read side of case search. Kept separate from the repository on purpose:
// search is a projection over many rows with paging and dynamic sort, not an
// aggregate load, and the infrastructure implements it with raw SQL.
public interface ICaseSearchQueries
{
    Task<PagedResult<CaseSummary>> SearchAsync(SearchCasesQuery query, CancellationToken cancellationToken = default);
}

using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Queries;
using CaseFlow.Application.Common;
using FluentValidation;

namespace CaseFlow.Application.Cases.Handlers;

public sealed class SearchCasesHandler(
    ICaseSearchQueries searchQueries,
    ICurrentUser currentUser,
    IValidator<SearchCasesQuery> validator) : IQueryHandler<SearchCasesQuery, PagedResult<CaseSummary>>
{
    public async Task<PagedResult<CaseSummary>> HandleAsync(SearchCasesQuery query, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);

        // Non-admins are always scoped to their own organization, whatever
        // the query string asked for. Admins may filter across orgs.
        var scoped = currentUser.IsAdmin
            ? query
            : query with { OrganizationId = currentUser.OrganizationId };

        return await searchQueries.SearchAsync(scoped, cancellationToken);
    }
}

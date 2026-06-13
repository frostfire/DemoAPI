using CaseFlow.Domain.Cases;

namespace CaseFlow.Application.Cases.Queries;

public sealed record SearchCasesQuery(
    string? OrganizationId,
    CaseStatus? Status,
    CasePriority? Priority,
    string? SearchTerm,
    int Page = 1,
    int PageSize = 25,
    string Sort = "-createdAt");

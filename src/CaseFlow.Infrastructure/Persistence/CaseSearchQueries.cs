using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Queries;
using CaseFlow.Application.Common;
using CaseFlow.Domain.Cases;
using Dapper;
using Microsoft.EntityFrameworkCore;

namespace CaseFlow.Infrastructure.Persistence;

// This is the one deliberate Dapper query in the codebase. Everything else
// goes through EF Core, which earns its keep on writes (change tracking,
// migrations, the phase 4 outbox transaction). Search is the opposite case:
// a flat projection with dynamic sort and paging, where hand-written SQL is
// shorter, obvious, and easy to EXPLAIN against the real database.
public sealed class CaseSearchQueries(CaseFlowDbContext dbContext) : ICaseSearchQueries
{
    // Sort keys are mapped through this whitelist - user input never reaches
    // the ORDER BY clause as a string.
    private static readonly Dictionary<string, string> SortColumns = new()
    {
        ["createdAt"] = "\"CreatedAt\"",
        ["title"] = "\"Title\"",
        ["priority"] = "\"Priority\"",
        ["status"] = "\"Status\"",
    };

    public async Task<PagedResult<CaseSummary>> SearchAsync(SearchCasesQuery query, CancellationToken cancellationToken = default)
    {
        var descending = query.Sort.StartsWith('-');
        var sortColumn = SortColumns[query.Sort.TrimStart('-')];
        var direction = descending ? "DESC" : "ASC";

        // COUNT(*) OVER() returns the unpaged total on every row, which keeps
        // this to a single round trip instead of a separate count query.
        var sql = $"""
            SELECT "Id", "OrganizationId", "Title", "Description", "Status", "Priority",
                   "RejectReason", "CreatedByUserId", "SubmittedByUserId", "ReviewedByUserId",
                   "CreatedAt", "UpdatedAt", "SubmittedAt", "ReviewedAt",
                   COUNT(*) OVER() AS "TotalCount"
            FROM "Cases"
            WHERE (@OrganizationId IS NULL OR "OrganizationId" = @OrganizationId)
              AND (@Status IS NULL OR "Status" = @Status)
              AND (@Priority IS NULL OR "Priority" = @Priority)
              AND (@SearchTerm IS NULL OR "Title" ILIKE '%' || @SearchTerm || '%')
            ORDER BY {sortColumn} {direction}, "Id"
            LIMIT @PageSize OFFSET @Offset
            """;

        var connection = dbContext.Database.GetDbConnection();
        var rows = await connection.QueryAsync<CaseRow>(new CommandDefinition(
            sql,
            new
            {
                query.OrganizationId,
                Status = query.Status?.ToString(),
                Priority = query.Priority?.ToString(),
                query.SearchTerm,
                query.PageSize,
                Offset = (query.Page - 1) * query.PageSize,
            },
            cancellationToken: cancellationToken));

        var rowList = rows.ToList();
        var totalCount = rowList.Count > 0 ? (int)rowList[0].TotalCount : 0;
        var items = rowList.Select(r => r.ToSummary()).ToList();

        return new PagedResult<CaseSummary>(items, query.Page, query.PageSize, totalCount);
    }

    // Dapper materializes into this row type with plain CLR types, and the
    // conversions to enums/DateTimeOffset happen explicitly below - no type
    // handler magic to debug later.
    private sealed record CaseRow(
        Guid Id,
        string OrganizationId,
        string Title,
        string? Description,
        string Status,
        string Priority,
        string? RejectReason,
        string CreatedByUserId,
        string? SubmittedByUserId,
        string? ReviewedByUserId,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? SubmittedAt,
        DateTime? ReviewedAt,
        long TotalCount)
    {
        public CaseSummary ToSummary() => new(
            Id,
            OrganizationId,
            Title,
            Description,
            Enum.Parse<CaseStatus>(Status),
            Enum.Parse<CasePriority>(Priority),
            RejectReason,
            CreatedByUserId,
            SubmittedByUserId,
            ReviewedByUserId,
            new DateTimeOffset(CreatedAt, TimeSpan.Zero),
            new DateTimeOffset(UpdatedAt, TimeSpan.Zero),
            SubmittedAt is null ? null : new DateTimeOffset(SubmittedAt.Value, TimeSpan.Zero),
            ReviewedAt is null ? null : new DateTimeOffset(ReviewedAt.Value, TimeSpan.Zero));
    }
}

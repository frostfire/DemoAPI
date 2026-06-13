using CaseFlow.Application.Abstractions;
using CaseFlow.Domain.Cases;
using Microsoft.EntityFrameworkCore;

namespace CaseFlow.Infrastructure.Persistence;

public sealed class CaseAuditQueries(CaseFlowDbContext dbContext) : ICaseAuditQueries
{
    public async Task<IReadOnlyList<CaseAuditEntry>> GetForCaseAsync(Guid caseId, CancellationToken cancellationToken = default)
    {
        return await dbContext.CaseAuditLog
            .AsNoTracking()
            .Where(a => a.CaseId == caseId)
            .OrderByDescending(a => a.OccurredAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);
    }
}

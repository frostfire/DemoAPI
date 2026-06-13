using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Exceptions;
using CaseFlow.Domain.Cases;
using Microsoft.EntityFrameworkCore;

namespace CaseFlow.Infrastructure.Persistence;

public sealed class CaseRepository(CaseFlowDbContext dbContext) : ICaseRepository
{
    public Task<Case?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Cases.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<int> CountByOrganizationAsync(string organizationId, CancellationToken cancellationToken = default)
        => dbContext.Cases.CountAsync(c => c.OrganizationId == organizationId, cancellationToken);

    public void Add(Case @case) => dbContext.Cases.Add(@case);

    public void Remove(Case @case) => dbContext.Cases.Remove(@case);

    public void SetExpectedVersion(Case @case, uint expectedVersion)
    {
        // EF compares the original value against the row at UPDATE time; by
        // replacing it with what the client presented, a stale ETag turns
        // into a concurrency conflict instead of a silent overwrite.
        dbContext.Entry(@case).Property(c => c.Version).OriginalValue = expectedVersion;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException();
        }
    }
}

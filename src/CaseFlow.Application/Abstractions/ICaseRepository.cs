using CaseFlow.Domain.Cases;

namespace CaseFlow.Application.Abstractions;

// Single-aggregate repository, so the unit of work is folded into it for now.
// When the outbox lands in phase 4, the shared transaction boundary becomes
// its own abstraction - that's the point where splitting it earns its keep.
public interface ICaseRepository
{
    Task<Case?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountByOrganizationAsync(string organizationId, CancellationToken cancellationToken = default);
    void Add(Case @case);
    void Remove(Case @case);

    // Arms the optimistic concurrency check: SaveChangesAsync throws
    // ConcurrencyConflictException if the stored row version no longer
    // matches the one the client presented via If-Match.
    void SetExpectedVersion(Case @case, uint expectedVersion);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

using CaseFlow.Application.Abstractions;
using CaseFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CaseFlow.Infrastructure.Idempotency;

public sealed class EfIdempotencyStore(CaseFlowDbContext dbContext, TimeProvider clock) : IIdempotencyStore
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromHours(24);

    public async Task<StoredIdempotentResponse?> FindAsync(string key, CancellationToken cancellationToken = default)
    {
        var record = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Key == key, cancellationToken);

        // Expired records are treated as absent; the phase 5 cleanup job
        // deletes them for real.
        if (record is null || record.ExpiresAt < clock.GetUtcNow())
        {
            return null;
        }

        return new StoredIdempotentResponse(record.RequestHash, record.StatusCode, record.ResponseBody);
    }

    public async Task SaveAsync(string key, string requestHash, int statusCode, string responseBody, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        dbContext.IdempotencyRecords.Add(
            new IdempotencyRecord(key, requestHash, statusCode, responseBody, now, now.Add(RetentionPeriod)));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // Two requests raced on the same key and the other one won the
            // insert. Fine - the stored response is theirs, and identical
            // requests produce identical responses anyway.
        }
    }
}

namespace CaseFlow.Application.Abstractions;

// Backing store for Idempotency-Key handling. The API layer owns the HTTP
// mechanics (header, replay, response capture); this just remembers what a
// key produced the first time.
public interface IIdempotencyStore
{
    Task<StoredIdempotentResponse?> FindAsync(string key, CancellationToken cancellationToken = default);

    Task SaveAsync(
        string key,
        string requestHash,
        int statusCode,
        string responseBody,
        CancellationToken cancellationToken = default);
}

public sealed record StoredIdempotentResponse(string RequestHash, int StatusCode, string ResponseBody);

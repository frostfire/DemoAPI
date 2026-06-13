namespace CaseFlow.Infrastructure.Idempotency;

public class IdempotencyRecord
{
    private IdempotencyRecord() { }

    public IdempotencyRecord(string key, string requestHash, int statusCode, string responseBody, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Key = key;
        RequestHash = requestHash;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public string Key { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public int StatusCode { get; private set; }
    public string ResponseBody { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
}

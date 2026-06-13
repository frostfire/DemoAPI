using System.Text.Json;
using CaseFlow.Domain.Cases.Events;

namespace CaseFlow.Infrastructure.Outbox;

// The reliability half of the outbox pattern: the message is inserted in the
// same transaction as the state change it describes, so either both happen
// or neither does. The worker delivers it later and marks it processed.
public class OutboxMessage
{
    private OutboxMessage() { }

    public Guid Id { get; private set; }
    public string Type { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }

    public static OutboxMessage FromEvent(IDomainEvent domainEvent)
    {
        return new OutboxMessage
        {
            Id = domainEvent.EventId,
            Type = domainEvent.GetType().Name,
            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
            OccurredAt = domainEvent.OccurredAt,
        };
    }

    public void MarkProcessed(DateTimeOffset utcNow) => ProcessedAt = utcNow;

    public void MarkFailed(string error, DateTimeOffset utcNow)
    {
        Attempts++;
        // Keep the error short - this is a diagnostic breadcrumb, not a log.
        LastError = error.Length > 500 ? error[..500] : error;
        // Exponential backoff: 30s, 1m, 2m, 4m... capped at an hour.
        var delay = TimeSpan.FromSeconds(Math.Min(30 * Math.Pow(2, Attempts - 1), 3600));
        NextAttemptAt = utcNow.Add(delay);
    }
}

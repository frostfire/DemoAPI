using System.Text.Json;
using CaseFlow.Application.Abstractions;
using CaseFlow.Domain.Cases.Events;
using CaseFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CaseFlow.Infrastructure.Outbox;

// Drains the outbox: pick up unprocessed messages, dispatch, mark processed.
// A failure marks the message with an error and an exponential-backoff next
// attempt instead of blocking the batch. Separate from the hosted service
// loop so tests can drive a batch synchronously.
//
// Single-instance assumptions, documented on purpose: with multiple workers
// this query would need FOR UPDATE SKIP LOCKED, and that is exactly the kind
// of change phase 5's Hangfire migration absorbs.
public sealed class OutboxBatchProcessor(
    CaseFlowDbContext dbContext,
    INotificationSender notificationSender,
    TimeProvider clock,
    ILogger<OutboxBatchProcessor> logger)
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 8;

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var batch = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt == null
                && m.Attempts < MaxAttempts
                && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in batch)
        {
            try
            {
                await DispatchAsync(message, cancellationToken);
                message.MarkProcessed(clock.GetUtcNow());
            }
            catch (Exception ex)
            {
                message.MarkFailed(ex.Message, clock.GetUtcNow());
                logger.LogWarning(ex,
                    "Outbox message {MessageId} ({Type}) failed on attempt {Attempt}",
                    message.Id, message.Type, message.Attempts);
            }
        }

        if (batch.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return batch.Count;
    }

    private async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        // Only decision events fan out as notifications today. Everything
        // else is still worth keeping in the outbox - a future integration
        // (message bus, webhooks, search indexer) replays from here.
        switch (message.Type)
        {
            case nameof(CaseApproved):
            {
                var evt = JsonSerializer.Deserialize<CaseApproved>(message.Payload)!;
                await notificationSender.SendCaseDecisionAsync(
                    evt.CaseId, evt.SubmittedByUserId, "approved", null, cancellationToken);
                break;
            }
            case nameof(CaseRejected):
            {
                var evt = JsonSerializer.Deserialize<CaseRejected>(message.Payload)!;
                await notificationSender.SendCaseDecisionAsync(
                    evt.CaseId, evt.SubmittedByUserId, "rejected", evt.Reason, cancellationToken);
                break;
            }
            default:
                logger.LogDebug("Outbox message {MessageId} ({Type}) has no handler - marked processed",
                    message.Id, message.Type);
                break;
        }
    }
}

using CaseFlow.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace CaseFlow.Infrastructure.Notifications;

// Fake by design. The demo proves the delivery guarantee (outbox -> worker
// -> sender, with retries); wiring a real SMTP/webhook sender here would
// add secrets and infrastructure without demonstrating anything new.
public sealed class FakeNotificationSender(ILogger<FakeNotificationSender> logger) : INotificationSender
{
    public Task SendCaseDecisionAsync(
        Guid caseId,
        string recipientUserId,
        string decision,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Notification: case {CaseId} was {Decision} - telling {RecipientUserId}{ReasonSuffix}",
            caseId,
            decision,
            recipientUserId,
            reason is null ? "" : $" (reason: {reason})");

        return Task.CompletedTask;
    }
}

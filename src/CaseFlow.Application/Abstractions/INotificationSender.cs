namespace CaseFlow.Application.Abstractions;

// The outbox processor's downstream. The implementation is fake by design -
// it logs instead of sending - because this repo demonstrates the delivery
// guarantee, not an SMTP integration. Swapping in a real sender changes one
// class and zero callers.
public interface INotificationSender
{
    Task SendCaseDecisionAsync(
        Guid caseId,
        string recipientUserId,
        string decision,
        string? reason,
        CancellationToken cancellationToken = default);
}

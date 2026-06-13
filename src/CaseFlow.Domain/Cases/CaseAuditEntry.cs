using CaseFlow.Domain.Cases.Events;

namespace CaseFlow.Domain.Cases;

// One row per domain event, written in the same transaction as the case
// change. The audit log is append-only: nothing in the codebase updates or
// deletes these rows.
public class CaseAuditEntry
{
    private CaseAuditEntry() { }

    public Guid Id { get; private set; }
    public Guid CaseId { get; private set; }
    public string OrganizationId { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string PerformedByUserId { get; private set; } = null!;
    public string? Details { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static CaseAuditEntry FromEvent(IDomainEvent domainEvent)
    {
        return new CaseAuditEntry
        {
            Id = Guid.CreateVersion7(),
            CaseId = domainEvent.CaseId,
            OrganizationId = domainEvent.OrganizationId,
            // "CaseSubmitted" -> "Submitted"; the table is already about cases.
            Action = domainEvent.GetType().Name is var name && name.StartsWith("Case")
                ? name[4..]
                : name,
            PerformedByUserId = domainEvent.PerformedByUserId,
            Details = domainEvent switch
            {
                CaseRejected rejected => rejected.Reason,
                _ => null,
            },
            OccurredAt = domainEvent.OccurredAt,
        };
    }
}

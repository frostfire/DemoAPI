namespace CaseFlow.Domain.Cases.Events;

// Domain events are facts: something that already happened, named in the
// past tense, carrying everything a consumer needs. The persistence layer
// turns each one into an audit entry and an outbox message in the same
// transaction as the state change itself.
public interface IDomainEvent
{
    Guid EventId { get; }
    Guid CaseId { get; }
    string OrganizationId { get; }
    string PerformedByUserId { get; }
    DateTimeOffset OccurredAt { get; }
}

public abstract record CaseEvent(
    Guid CaseId,
    string OrganizationId,
    string PerformedByUserId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public Guid EventId { get; } = Guid.CreateVersion7();
}

public sealed record CaseCreated(
    Guid CaseId,
    string OrganizationId,
    string PerformedByUserId,
    DateTimeOffset OccurredAt,
    string Title,
    CasePriority Priority) : CaseEvent(CaseId, OrganizationId, PerformedByUserId, OccurredAt);

public sealed record CaseUpdated(
    Guid CaseId,
    string OrganizationId,
    string PerformedByUserId,
    DateTimeOffset OccurredAt) : CaseEvent(CaseId, OrganizationId, PerformedByUserId, OccurredAt);

public sealed record CaseSubmitted(
    Guid CaseId,
    string OrganizationId,
    string PerformedByUserId,
    DateTimeOffset OccurredAt) : CaseEvent(CaseId, OrganizationId, PerformedByUserId, OccurredAt);

public sealed record CaseApproved(
    Guid CaseId,
    string OrganizationId,
    string PerformedByUserId,
    DateTimeOffset OccurredAt,
    string SubmittedByUserId) : CaseEvent(CaseId, OrganizationId, PerformedByUserId, OccurredAt);

public sealed record CaseRejected(
    Guid CaseId,
    string OrganizationId,
    string PerformedByUserId,
    DateTimeOffset OccurredAt,
    string Reason,
    string SubmittedByUserId) : CaseEvent(CaseId, OrganizationId, PerformedByUserId, OccurredAt);

public sealed record CaseReopened(
    Guid CaseId,
    string OrganizationId,
    string PerformedByUserId,
    DateTimeOffset OccurredAt) : CaseEvent(CaseId, OrganizationId, PerformedByUserId, OccurredAt);

public sealed record CaseArchived(
    Guid CaseId,
    string OrganizationId,
    string PerformedByUserId,
    DateTimeOffset OccurredAt) : CaseEvent(CaseId, OrganizationId, PerformedByUserId, OccurredAt);

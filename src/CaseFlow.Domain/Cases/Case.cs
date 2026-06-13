using CaseFlow.Domain.Cases.Events;
using CaseFlow.Domain.Exceptions;

namespace CaseFlow.Domain.Cases;

// The workflow lives here, not in the handlers. Every transition is a named
// method with its own guard, so an illegal move fails loudly with the exact
// state that rejected it instead of leaving a half-updated row behind.
// Each successful transition raises a domain event; persistence turns those
// into audit entries and outbox messages inside the same transaction.
//
// Valid transitions:
//   Draft         -> Submit  -> PendingReview
//   PendingReview -> Approve -> Approved
//   PendingReview -> Reject  -> Rejected
//   Rejected      -> Reopen  -> Reopened
//   Reopened      -> Submit  -> PendingReview
//   Approved      -> Archive -> Archived
public class Case
{
    private readonly List<IDomainEvent> _domainEvents = [];

    // EF Core materializes through this; everyone else goes through Create().
    private Case() { }

    public Guid Id { get; private set; }
    public string OrganizationId { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string? Description { get; private set; }
    public CasePriority Priority { get; private set; }
    public CaseStatus Status { get; private set; }
    public string? RejectReason { get; private set; }
    public string CreatedByUserId { get; private set; } = null!;
    public string? SubmittedByUserId { get; private set; }
    public string? ReviewedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }

    // Mapped to Postgres' xmin system column - the row version every UPDATE
    // bumps for free. Exposed to clients as an ETag.
    public uint Version { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    public static Case Create(
        string organizationId,
        string createdByUserId,
        string title,
        string? description,
        CasePriority priority,
        DateTimeOffset utcNow)
    {
        var @case = new Case
        {
            // Version 7 GUIDs are time-ordered, which keeps the primary key
            // index append-mostly instead of fragmenting on random inserts.
            Id = Guid.CreateVersion7(),
            OrganizationId = organizationId,
            CreatedByUserId = createdByUserId,
            Title = title,
            Description = description,
            Priority = priority,
            Status = CaseStatus.Draft,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        @case._domainEvents.Add(new CaseCreated(
            @case.Id, organizationId, createdByUserId, utcNow, title, priority));

        return @case;
    }

    public void UpdateDetails(string userId, string title, string? description, CasePriority priority, DateTimeOffset utcNow)
    {
        RequireStatus("edited", CaseStatus.Draft, CaseStatus.Reopened);

        Title = title;
        Description = description;
        Priority = priority;
        UpdatedAt = utcNow;

        _domainEvents.Add(new CaseUpdated(Id, OrganizationId, userId, utcNow));
    }

    public void Submit(string userId, DateTimeOffset utcNow)
    {
        RequireStatus("submitted", CaseStatus.Draft, CaseStatus.Reopened);

        Status = CaseStatus.PendingReview;
        SubmittedByUserId = userId;
        SubmittedAt = utcNow;
        UpdatedAt = utcNow;

        _domainEvents.Add(new CaseSubmitted(Id, OrganizationId, userId, utcNow));
    }

    public void Approve(string userId, DateTimeOffset utcNow)
    {
        RequireStatus("approved", CaseStatus.PendingReview);

        // Separation of duties - submitting and approving must be two people.
        if (userId == SubmittedByUserId)
        {
            throw new SelfApprovalNotAllowedException();
        }

        Status = CaseStatus.Approved;
        ReviewedByUserId = userId;
        ReviewedAt = utcNow;
        UpdatedAt = utcNow;

        _domainEvents.Add(new CaseApproved(Id, OrganizationId, userId, utcNow, SubmittedByUserId!));
    }

    public void Reject(string userId, string reason, DateTimeOffset utcNow)
    {
        RequireStatus("rejected", CaseStatus.PendingReview);

        Status = CaseStatus.Rejected;
        RejectReason = reason;
        ReviewedByUserId = userId;
        ReviewedAt = utcNow;
        UpdatedAt = utcNow;

        _domainEvents.Add(new CaseRejected(Id, OrganizationId, userId, utcNow, reason, SubmittedByUserId!));
    }

    public void Reopen(string userId, DateTimeOffset utcNow)
    {
        RequireStatus("reopened", CaseStatus.Rejected);

        Status = CaseStatus.Reopened;
        UpdatedAt = utcNow;

        _domainEvents.Add(new CaseReopened(Id, OrganizationId, userId, utcNow));
    }

    public void Archive(string userId, DateTimeOffset utcNow)
    {
        RequireStatus("archived", CaseStatus.Approved);

        Status = CaseStatus.Archived;
        UpdatedAt = utcNow;

        _domainEvents.Add(new CaseArchived(Id, OrganizationId, userId, utcNow));
    }

    // Only drafts can be deleted outright. Anything that entered review has
    // an audit history worth keeping.
    public bool CanBeDeleted => Status == CaseStatus.Draft;

    private void RequireStatus(string attemptedAction, params CaseStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidCaseTransitionException(Status, attemptedAction);
        }
    }
}

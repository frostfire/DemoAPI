namespace CaseFlow.Contracts.Cases;

// These mirror the domain enums on purpose. The duplication is the feature:
// the wire contract can stay stable even if the internal model changes, and
// the domain never leaks into a published client package.

public enum CaseStatus
{
    Draft,
    PendingReview,
    Approved,
    Rejected,
    Reopened,
    Archived
}

public enum CasePriority
{
    Low,
    Normal,
    High,
    Urgent
}

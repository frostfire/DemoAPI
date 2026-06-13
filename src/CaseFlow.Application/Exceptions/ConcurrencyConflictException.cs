namespace CaseFlow.Application.Exceptions;

// Thrown when a save loses an optimistic concurrency race - the row changed
// between the client's read and this write. Maps to 412 Precondition Failed.
public sealed class ConcurrencyConflictException()
    : Exception("The case was modified by someone else. Reload it and retry with the current ETag.");

namespace CaseFlow.Domain.Exceptions;

// Separation of duties: the person who submitted a case never approves it,
// no matter what roles they hold. This is a domain rule rather than an
// authorization policy because it depends on the case's own history, not
// just on who the caller is.
public sealed class SelfApprovalNotAllowedException()
    : DomainException("A case cannot be approved by the user who submitted it.");

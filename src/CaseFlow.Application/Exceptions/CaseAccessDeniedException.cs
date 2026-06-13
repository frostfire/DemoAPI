namespace CaseFlow.Application.Exceptions;

// Deliberately vague message - the response must not confirm whose case it
// is or that it exists in another organization.
public sealed class CaseAccessDeniedException()
    : Exception("You do not have access to this case.");

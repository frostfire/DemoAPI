namespace CaseFlow.Domain.Exceptions;

// Base for every rule the domain enforces. The API layer maps these to
// ProblemDetails responses; nothing in Domain knows or cares about HTTP.
public abstract class DomainException(string message) : Exception(message);

namespace CaseFlow.Application.Exceptions;

// Raised when a demo organization hits its case cap. Maps to 409 - the request
// is well-formed, it just conflicts with the sandbox quota.
public sealed class DemoQuotaExceededException(int limit)
    : Exception($"This demo organization has reached its limit of {limit} cases. The sandbox resets periodically.")
{
    public int Limit { get; } = limit;
}

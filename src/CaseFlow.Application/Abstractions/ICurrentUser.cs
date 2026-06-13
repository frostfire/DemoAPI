namespace CaseFlow.Application.Abstractions;

// What the application layer knows about the caller. Populated from JWT
// claims by the API host - handlers never touch HttpContext directly, which
// keeps them testable with a plain fake.
public interface ICurrentUser
{
    string UserId { get; }
    string OrganizationId { get; }
    bool IsAdmin { get; }
}

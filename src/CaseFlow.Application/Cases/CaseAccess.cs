using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Exceptions;
using CaseFlow.Domain.Cases;

namespace CaseFlow.Application.Cases;

// Object-level authorization: role policies decide what kind of action a
// caller may attempt, this decides whether they may touch this particular
// case. Every handler that loads a case by id goes through here.
public static class CaseAccess
{
    public static Case EnsureAccessibleBy(this Case @case, ICurrentUser user)
    {
        if (!user.IsAdmin && @case.OrganizationId != user.OrganizationId)
        {
            // 403 rather than 404 is a deliberate choice for this demo: the
            // ids are GUIDs (not enumerable) and an explicit "forbidden" makes
            // the org boundary visible to reviewers. Systems with guessable
            // ids should return 404 here to avoid confirming existence.
            throw new CaseAccessDeniedException();
        }

        return @case;
    }
}

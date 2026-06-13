using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Commands;
using CaseFlow.Application.Exceptions;
using CaseFlow.Domain.Exceptions;

namespace CaseFlow.Application.Cases.Handlers;

public sealed class DeleteCaseHandler(ICaseRepository repository, ICurrentUser currentUser)
    : ICommandHandler<DeleteCaseCommand, bool>
{
    public async Task<bool> HandleAsync(DeleteCaseCommand command, CancellationToken cancellationToken = default)
    {
        var @case = await repository.GetAsync(command.CaseId, cancellationToken)
            ?? throw new CaseNotFoundException(command.CaseId);
        @case.EnsureAccessibleBy(currentUser);

        if (!@case.CanBeDeleted)
        {
            throw new InvalidCaseTransitionException(@case.Status, "deleted");
        }

        repository.Remove(@case);
        await repository.SaveChangesAsync(cancellationToken);

        return true;
    }
}

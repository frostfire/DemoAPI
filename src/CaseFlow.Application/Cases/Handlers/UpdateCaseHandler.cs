using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Commands;
using CaseFlow.Application.Exceptions;
using CaseFlow.Domain.Cases;
using FluentValidation;

namespace CaseFlow.Application.Cases.Handlers;

public sealed class UpdateCaseHandler(
    ICaseRepository repository,
    ICurrentUser currentUser,
    IValidator<UpdateCaseCommand> validator,
    TimeProvider clock) : ICommandHandler<UpdateCaseCommand, Case>
{
    public async Task<Case> HandleAsync(UpdateCaseCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var @case = await repository.GetAsync(command.CaseId, cancellationToken)
            ?? throw new CaseNotFoundException(command.CaseId);
        @case.EnsureAccessibleBy(currentUser);

        if (command.ExpectedVersion is { } expected)
        {
            repository.SetExpectedVersion(@case, expected);
        }

        @case.UpdateDetails(currentUser.UserId, command.Title, command.Description, command.Priority, clock.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return @case;
    }
}

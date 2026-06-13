using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Commands;
using CaseFlow.Application.Demo;
using CaseFlow.Application.Exceptions;
using CaseFlow.Domain.Cases;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CaseFlow.Application.Cases.Handlers;

public sealed class CreateCaseHandler(
    ICaseRepository repository,
    ICurrentUser currentUser,
    IValidator<CreateCaseCommand> validator,
    IOptions<DemoOptions> demoOptions,
    TimeProvider clock) : ICommandHandler<CreateCaseCommand, Case>
{
    public async Task<Case> HandleAsync(CreateCaseCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        // Sandbox quota: only enforced in demo mode, keeps a visitor from
        // filling the throwaway database between resets.
        var demo = demoOptions.Value;
        if (demo is { Enabled: true, MaxCasesPerOrganization: > 0 })
        {
            var existing = await repository.CountByOrganizationAsync(currentUser.OrganizationId, cancellationToken);
            if (existing >= demo.MaxCasesPerOrganization)
            {
                throw new DemoQuotaExceededException(demo.MaxCasesPerOrganization);
            }
        }

        var @case = Case.Create(
            currentUser.OrganizationId,
            currentUser.UserId,
            command.Title,
            command.Description,
            command.Priority,
            clock.GetUtcNow());

        repository.Add(@case);
        await repository.SaveChangesAsync(cancellationToken);

        return @case;
    }
}

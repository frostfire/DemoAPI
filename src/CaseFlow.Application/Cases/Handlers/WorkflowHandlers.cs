using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases;
using CaseFlow.Application.Cases.Commands;
using CaseFlow.Application.Exceptions;
using CaseFlow.Domain.Cases;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CaseFlow.Application.Cases.Handlers;

// The four transition handlers share a load-check-mutate-save shape, but they
// stay separate classes: each one has different authorization at the API
// layer, and phase 4 gives each one a different audit entry and outbox
// message. Merging them now would just mean un-merging them later.

public sealed class SubmitCaseHandler(
    ICaseRepository repository,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<SubmitCaseCommand, Case>
{
    public async Task<Case> HandleAsync(SubmitCaseCommand command, CancellationToken cancellationToken = default)
    {
        var @case = await repository.GetAsync(command.CaseId, cancellationToken)
            ?? throw new CaseNotFoundException(command.CaseId);
        @case.EnsureAccessibleBy(currentUser);

        @case.Submit(currentUser.UserId, clock.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return @case;
    }
}

public sealed class ApproveCaseHandler(
    ICaseRepository repository,
    ICurrentUser currentUser,
    IJobScheduler jobScheduler,
    IOptions<WorkflowOptions> workflowOptions,
    TimeProvider clock) : ICommandHandler<ApproveCaseCommand, Case>
{
    public async Task<Case> HandleAsync(ApproveCaseCommand command, CancellationToken cancellationToken = default)
    {
        var @case = await repository.GetAsync(command.CaseId, cancellationToken)
            ?? throw new CaseNotFoundException(command.CaseId);
        @case.EnsureAccessibleBy(currentUser);

        // The self-approval check happens inside Approve() - the domain owns
        // that rule because it depends on the case's history.
        @case.Approve(currentUser.UserId, clock.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        // Hand the delayed archive to the scheduler. This runs after the
        // commit rather than through the outbox: the archive is advisory and
        // the job is idempotent, so a missed schedule is harmless where a
        // missed notification would not be.
        jobScheduler.ScheduleCaseAutoArchive(@case.Id, workflowOptions.Value.AutoArchiveAfter);

        return @case;
    }
}

public sealed class RejectCaseHandler(
    ICaseRepository repository,
    ICurrentUser currentUser,
    IValidator<RejectCaseCommand> validator,
    TimeProvider clock) : ICommandHandler<RejectCaseCommand, Case>
{
    public async Task<Case> HandleAsync(RejectCaseCommand command, CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var @case = await repository.GetAsync(command.CaseId, cancellationToken)
            ?? throw new CaseNotFoundException(command.CaseId);
        @case.EnsureAccessibleBy(currentUser);

        @case.Reject(currentUser.UserId, command.Reason, clock.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return @case;
    }
}

public sealed class ReopenCaseHandler(
    ICaseRepository repository,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<ReopenCaseCommand, Case>
{
    public async Task<Case> HandleAsync(ReopenCaseCommand command, CancellationToken cancellationToken = default)
    {
        var @case = await repository.GetAsync(command.CaseId, cancellationToken)
            ?? throw new CaseNotFoundException(command.CaseId);
        @case.EnsureAccessibleBy(currentUser);

        @case.Reopen(currentUser.UserId, clock.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return @case;
    }
}

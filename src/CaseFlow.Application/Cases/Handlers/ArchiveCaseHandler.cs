using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Commands;
using CaseFlow.Domain.Cases;

namespace CaseFlow.Application.Cases.Handlers;

public sealed class ArchiveCaseHandler(ICaseRepository repository, TimeProvider clock)
    : ICommandHandler<ArchiveCaseCommand, bool>
{
    public const string SystemActor = "system";

    public async Task<bool> HandleAsync(ArchiveCaseCommand command, CancellationToken cancellationToken = default)
    {
        var @case = await repository.GetAsync(command.CaseId, cancellationToken);

        // Idempotent on purpose: by the time the delayed job fires the case
        // may already be archived, or it may never have reached Approved (the
        // only state Archive accepts). Either way, skip quietly rather than
        // throw - a background job has no caller to report a 409 to.
        if (@case is null || @case.Status != CaseStatus.Approved)
        {
            return false;
        }

        @case.Archive(SystemActor, clock.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }
}

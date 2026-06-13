using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Commands;
using Microsoft.Extensions.Logging;

namespace CaseFlow.Infrastructure.Scheduling.Hangfire;

// A delayed Hangfire job: scheduled when a case is approved, fired once after
// the configured delay. The handler is idempotent, so a case that was already
// archived (or never stayed Approved) is simply skipped.
public sealed class CaseAutoArchiveJob(
    ICommandHandler<ArchiveCaseCommand, bool> archiveHandler,
    ILogger<CaseAutoArchiveJob> logger)
{
    public async Task RunAsync(Guid caseId)
    {
        var archived = await archiveHandler.HandleAsync(new ArchiveCaseCommand(caseId));
        logger.LogInformation(
            "Auto-archive job for case {CaseId}: {Outcome}",
            caseId,
            archived ? "archived" : "skipped (no longer eligible)");
    }
}

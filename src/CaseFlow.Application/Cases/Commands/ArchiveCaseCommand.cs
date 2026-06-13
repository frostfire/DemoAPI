namespace CaseFlow.Application.Cases.Commands;

// Archiving is a system action with no interactive caller, so the command
// carries no user - the handler records "system" as the actor. Only the
// scheduled auto-archive job issues this; it is never exposed over HTTP.
public sealed record ArchiveCaseCommand(Guid CaseId);

namespace CaseFlow.Application.Cases.Commands;

// The workflow commands are thin on purpose - the state rules live on the
// Case entity. These exist so each transition has its own handler, its own
// authorization surface (phase 3), and its own audit entry (phase 4).
public sealed record SubmitCaseCommand(Guid CaseId);

public sealed record ApproveCaseCommand(Guid CaseId);

public sealed record RejectCaseCommand(Guid CaseId, string Reason);

public sealed record ReopenCaseCommand(Guid CaseId);

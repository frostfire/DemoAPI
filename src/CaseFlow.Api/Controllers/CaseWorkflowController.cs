using CaseFlow.Api.Auth;
using CaseFlow.Api.Idempotency;
using CaseFlow.Api.Mapping;
using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Commands;
using CaseFlow.Contracts.Cases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DomainCase = CaseFlow.Domain.Cases.Case;

namespace CaseFlow.Api.Controllers;

// Workflow transitions are POST actions on the case, not PATCHes of a status
// field. A client cannot put a case into an arbitrary state - it can only ask
// for a named transition, and the domain decides whether that's legal.
// Each transition carries its own policy: writers submit and reopen,
// reviewers reject, approvers approve.
[ApiController]
[Route("api/v1/cases/{caseId:guid}")]
[Authorize]
[Tags("Workflow")]
public class CaseWorkflowController(
    ICommandHandler<SubmitCaseCommand, DomainCase> submitHandler,
    ICommandHandler<ApproveCaseCommand, DomainCase> approveHandler,
    ICommandHandler<RejectCaseCommand, DomainCase> rejectHandler,
    ICommandHandler<ReopenCaseCommand, DomainCase> reopenHandler) : ControllerBase
{
    [HttpPost("submit")]
    [Idempotent]
    [Authorize(Policy = Policies.CanWriteCases)]
    [ProducesResponseType<CaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CaseResponse>> Submit(Guid caseId, CancellationToken cancellationToken)
    {
        var @case = await submitHandler.HandleAsync(new SubmitCaseCommand(caseId), cancellationToken);

        return @case.ToResponse();
    }

    [HttpPost("approve")]
    [Idempotent]
    [Authorize(Policy = Policies.CanApproveCases)]
    [ProducesResponseType<CaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CaseResponse>> Approve(Guid caseId, CancellationToken cancellationToken)
    {
        var @case = await approveHandler.HandleAsync(new ApproveCaseCommand(caseId), cancellationToken);

        return @case.ToResponse();
    }

    [HttpPost("reject")]
    [Idempotent]
    [Authorize(Policy = Policies.CanReviewCases)]
    [ProducesResponseType<CaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CaseResponse>> Reject(Guid caseId, RejectCaseRequest request, CancellationToken cancellationToken)
    {
        var @case = await rejectHandler.HandleAsync(new RejectCaseCommand(caseId, request.Reason), cancellationToken);

        return @case.ToResponse();
    }

    [HttpPost("reopen")]
    [Authorize(Policy = Policies.CanWriteCases)]
    [ProducesResponseType<CaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CaseResponse>> Reopen(Guid caseId, CancellationToken cancellationToken)
    {
        var @case = await reopenHandler.HandleAsync(new ReopenCaseCommand(caseId), cancellationToken);

        return @case.ToResponse();
    }
}

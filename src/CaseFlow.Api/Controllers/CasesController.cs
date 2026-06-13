using CaseFlow.Api.Auth;
using CaseFlow.Api.Mapping;
using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Commands;
using CaseFlow.Application.Cases.Queries;
using CaseFlow.Application.Common;
using CaseFlow.Contracts.Cases;
using CaseFlow.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DomainCase = CaseFlow.Domain.Cases.Case;

namespace CaseFlow.Api.Controllers;

[ApiController]
[Route("api/v1/cases")]
[Authorize]
[Tags("Cases")]
public class CasesController(
    ICommandHandler<CreateCaseCommand, DomainCase> createHandler,
    ICommandHandler<UpdateCaseCommand, DomainCase> updateHandler,
    ICommandHandler<DeleteCaseCommand, bool> deleteHandler,
    IQueryHandler<GetCaseQuery, DomainCase> getHandler,
    IQueryHandler<SearchCasesQuery, PagedResult<CaseSummary>> searchHandler) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = Policies.CanWriteCases)]
    [ProducesResponseType<CaseResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CaseResponse>> Create(CreateCaseRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateCaseCommand(
            request.Title,
            request.Description,
            request.Priority.ToDomain());

        var created = await createHandler.HandleAsync(command, cancellationToken);

        Response.Headers.ETag = created.ToETag();
        return CreatedAtAction(nameof(Get), new { caseId = created.Id }, created.ToResponse());
    }

    [HttpGet("{caseId:guid}")]
    [Authorize(Policy = Policies.CanReadCases)]
    [ProducesResponseType<CaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaseResponse>> Get(Guid caseId, CancellationToken cancellationToken)
    {
        var @case = await getHandler.HandleAsync(new GetCaseQuery(caseId), cancellationToken);

        Response.Headers.ETag = @case.ToETag();
        return @case.ToResponse();
    }

    [HttpGet]
    [Authorize(Policy = Policies.CanReadCases)]
    [ProducesResponseType<PagedResponse<CaseResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<CaseResponse>>> Search(
        // organizationId only has an effect for admins; everyone else is
        // scoped to their own org by the handler regardless of what they ask.
        [FromQuery] string? organizationId,
        [FromQuery] CaseStatus? status,
        [FromQuery] CasePriority? priority,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string sort = "-createdAt",
        CancellationToken cancellationToken = default)
    {
        var query = new SearchCasesQuery(
            organizationId,
            status?.ToDomain(),
            priority?.ToDomain(),
            search,
            page,
            pageSize,
            sort);

        var result = await searchHandler.HandleAsync(query, cancellationToken);

        return new PagedResponse<CaseResponse>(
            result.Items.Select(i => i.ToResponse()).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);
    }

    // PATCH rather than PUT because only the draft-editable fields are
    // replaceable - status, timestamps, and reject reason are owned by the
    // workflow endpoints.
    [HttpPatch("{caseId:guid}")]
    [Authorize(Policy = Policies.CanWriteCases)]
    [ProducesResponseType<CaseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<CaseResponse>> Update(Guid caseId, UpdateCaseRequest request, CancellationToken cancellationToken)
    {
        // If-Match is optional: clients that send the ETag from their last
        // read get a 412 instead of overwriting someone else's change.
        uint? expectedVersion = null;
        var ifMatch = Request.Headers.IfMatch.FirstOrDefault();
        if (!string.IsNullOrEmpty(ifMatch) && ifMatch != "*")
        {
            var raw = ifMatch.Trim();
            raw = raw.StartsWith("W/", StringComparison.Ordinal) ? raw[2..] : raw;
            if (!uint.TryParse(raw.Trim('"'), out var parsed))
            {
                ModelState.AddModelError("If-Match", "If-Match must be an ETag previously returned for this case.");
                return ValidationProblem();
            }

            expectedVersion = parsed;
        }

        var command = new UpdateCaseCommand(caseId, request.Title, request.Description, request.Priority.ToDomain(), expectedVersion);
        var updated = await updateHandler.HandleAsync(command, cancellationToken);

        Response.Headers.ETag = updated.ToETag();
        return updated.ToResponse();
    }

    [HttpDelete("{caseId:guid}")]
    [Authorize(Policy = Policies.CanWriteCases)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid caseId, CancellationToken cancellationToken)
    {
        await deleteHandler.HandleAsync(new DeleteCaseCommand(caseId), cancellationToken);

        return NoContent();
    }
}

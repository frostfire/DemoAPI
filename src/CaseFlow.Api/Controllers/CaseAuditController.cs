using CaseFlow.Api.Auth;
using CaseFlow.Api.Mapping;
using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases.Queries;
using CaseFlow.Contracts.Cases;
using CaseFlow.Domain.Cases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CaseFlow.Api.Controllers;

[ApiController]
[Route("api/v1/cases/{caseId:guid}/audit")]
[Authorize]
[Tags("Audit")]
public class CaseAuditController(
    IQueryHandler<GetCaseAuditQuery, IReadOnlyList<CaseAuditEntry>> auditHandler) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = Policies.CanReadCases)]
    [ProducesResponseType<IReadOnlyList<CaseAuditEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CaseAuditEntryResponse>>> Get(Guid caseId, CancellationToken cancellationToken)
    {
        var entries = await auditHandler.HandleAsync(new GetCaseAuditQuery(caseId), cancellationToken);

        return entries.Select(e => e.ToResponse()).ToList();
    }
}

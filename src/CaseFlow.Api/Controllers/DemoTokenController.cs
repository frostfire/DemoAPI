using CaseFlow.Api.Auth;
using CaseFlow.Application.Demo;
using CaseFlow.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CaseFlow.Api.Controllers;

// Issues short-lived, role-scoped JWTs so anyone exploring the API can
// exercise the authorization rules without a registration flow. Gated by
// DemoAuth:Enabled - on in Development and on the public sandbox, off
// anywhere this code might run next to real data. A real deployment deletes
// this controller and points JwtBearer at its identity provider instead.
[ApiController]
[Route("api/v1/demo")]
[Tags("Demo")]
public class DemoTokenController(
    DemoTokenService tokenService,
    IOptions<DemoOptions> demoOptions,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("info")]
    [AllowAnonymous]
    [ProducesResponseType<DemoInfoResponse>(StatusCodes.Status200OK)]
    public ActionResult<DemoInfoResponse> Info()
    {
        var demo = demoOptions.Value;
        return new DemoInfoResponse(
            DemoMode: demo.Enabled,
            MaxCasesPerOrganization: demo.MaxCasesPerOrganization,
            ResetEvery: demo.Enabled ? demo.ResetEvery.ToString() : "n/a",
            AvailableRoles: CaseRoles.All,
            HowToAuthenticate: "POST /api/v1/demo/token with a 'roles' array, then send the returned token as 'Authorization: Bearer <token>'.");
    }

    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType<DemoTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<DemoTokenResponse> IssueToken(DemoTokenRequest request)
    {
        if (!configuration.GetValue<bool>("DemoAuth:Enabled"))
        {
            return NotFound();
        }

        if (request.Roles is not { Count: > 0 })
        {
            ModelState.AddModelError("roles", "At least one role is required.");
            return ValidationProblem();
        }

        var unknown = request.Roles.Except(CaseRoles.All, StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            ModelState.AddModelError(
                "roles",
                $"Unknown role(s): {string.Join(", ", unknown)}. Valid roles: {string.Join(", ", CaseRoles.All)}.");
            return ValidationProblem();
        }

        return tokenService.IssueToken(request.Roles, request.OrganizationId, request.UserId);
    }
}

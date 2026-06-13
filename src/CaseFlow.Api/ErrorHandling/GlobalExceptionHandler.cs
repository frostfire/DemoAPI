using System.Text.Json;
using CaseFlow.Application.Exceptions;
using CaseFlow.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CaseFlow.Api.ErrorHandling;

// One place where application and domain exceptions become HTTP. The mapping
// is intentionally explicit: 404 for missing resources, 409 for workflow
// conflicts, 422 for other broken business rules, 400 for validation.
// Anything unrecognized falls through to the framework's default 500 so we
// never leak internals in an error body.
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException ex => ValidationProblem(ex),
            CaseNotFoundException ex => Problem(StatusCodes.Status404NotFound, "Case not found", ex.Message),
            DemoQuotaExceededException ex => Problem(StatusCodes.Status409Conflict, "Demo quota reached", ex.Message),
            CaseAccessDeniedException ex => Problem(StatusCodes.Status403Forbidden, "Access denied", ex.Message),
            SelfApprovalNotAllowedException ex => Problem(StatusCodes.Status403Forbidden, "Approval not allowed", ex.Message),
            InvalidCaseTransitionException ex => Problem(StatusCodes.Status409Conflict, "Invalid case transition", ex.Message),
            ConcurrencyConflictException ex => Problem(StatusCodes.Status412PreconditionFailed, "Concurrency conflict", ex.Message),
            DomainException ex => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violated", ex.Message),
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail) => new()
    {
        Status = status,
        Title = title,
        Detail = detail,
    };

    private static ProblemDetails ValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Extensions = { ["errors"] = errors },
        };
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CaseFlow.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace CaseFlow.Api.Idempotency;

// Opt-in per action. When the caller sends an Idempotency-Key header, the
// first successful response is stored and any retry with the same key gets
// that exact response back instead of re-executing the action. A reused key
// with a different request is rejected - that's a client bug, not a retry.
[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentAttribute() : TypeFilterAttribute(typeof(IdempotencyFilter))
{
    public const string HeaderName = "Idempotency-Key";
}

public sealed class IdempotencyFilter(
    IIdempotencyStore store,
    IOptions<JsonOptions> jsonOptions) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = context.HttpContext.Request.Headers[IdempotentAttribute.HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            // The header is optional - without it the action just runs.
            await next();
            return;
        }

        if (key.Length > 128)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid Idempotency-Key",
                Detail = "Idempotency-Key must be 128 characters or fewer.",
            })
            { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }

        var cancellationToken = context.HttpContext.RequestAborted;
        var requestHash = ComputeRequestHash(context);

        var stored = await store.FindAsync(key, cancellationToken);
        if (stored is not null)
        {
            if (stored.RequestHash != requestHash)
            {
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Idempotency-Key reuse",
                    Detail = "This Idempotency-Key was already used for a different request.",
                })
                { StatusCode = StatusCodes.Status422UnprocessableEntity };
                return;
            }

            // Replay: same key, same request, same response - byte for byte.
            context.Result = new ContentResult
            {
                Content = stored.ResponseBody,
                ContentType = "application/json",
                StatusCode = stored.StatusCode,
            };
            return;
        }

        var executed = await next();

        // Only successful results are stored. A failed request is allowed to
        // be retried with the same key and actually re-execute - idempotency
        // protects against double-running a success, not against errors.
        if (executed.Exception is null
            && executed.Result is ObjectResult { Value: not null } objectResult
            && (objectResult.StatusCode ?? StatusCodes.Status200OK) is >= 200 and < 300)
        {
            var body = JsonSerializer.Serialize(objectResult.Value, jsonOptions.Value.JsonSerializerOptions);
            await store.SaveAsync(
                key,
                requestHash,
                objectResult.StatusCode ?? StatusCodes.Status200OK,
                body,
                cancellationToken);
        }
    }

    // Hashes method, path, and the bound action arguments rather than the
    // raw body stream - it's already consumed by model binding here, and the
    // bound values are what the action actually executes against.
    private static string ComputeRequestHash(ActionExecutingContext context)
    {
        var arguments = context.ActionArguments
            .Where(kvp => kvp.Value is not CancellationToken)
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        var material =
            $"{context.HttpContext.Request.Method}|{context.HttpContext.Request.Path}|{JsonSerializer.Serialize(arguments)}";

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }
}

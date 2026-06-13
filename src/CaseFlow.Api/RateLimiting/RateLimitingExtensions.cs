using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CaseFlow.Api.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int AnonymousPermitPerMinute { get; init; } = 30;
    public int AuthenticatedPermitPerMinute { get; init; } = 300;
    public int AdminPermitPerMinute { get; init; } = 1000;
}

// A fixed-window limiter partitioned by caller: authenticated requests by user
// id (admins get a higher ceiling), anonymous requests by IP. Applied only to
// the business controllers - health checks and the jobs dashboard are left
// unthrottled so probes and operators are never rate-limited.
//
// In-process only, which is the honest limit of this approach: behind multiple
// replicas you would back it with a shared store (Redis) so the window is
// global rather than per-instance. The hosted demo also layers proxy-level IP
// limiting on top (see docs/demo-hosting.md).
public static class RateLimitingExtensions
{
    public const string PerCallerPolicy = "per-caller";

    public static IServiceCollection AddCaseFlowRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PerCallerPolicy, httpContext =>
            {
                var limits = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitingOptions>>().Value;

                var userId = httpContext.User.FindFirst("sub")?.Value;
                if (userId is not null)
                {
                    var permit = httpContext.User.IsInRole("CaseAdmin")
                        ? limits.AdminPermitPerMinute
                        : limits.AuthenticatedPermitPerMinute;

                    return RateLimitPartition.GetFixedWindowLimiter(
                        $"user:{userId}",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = permit, Window = TimeSpan.FromMinutes(1) });
                }

                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"ip:{ip}",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = limits.AnonymousPermitPerMinute, Window = TimeSpan.FromMinutes(1) });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "Rate limit exceeded. Slow down and try again shortly.",
                    },
                    cancellationToken);
            };
        });

        return services;
    }
}

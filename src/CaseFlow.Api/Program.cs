using System.Text;
using System.Text.Json.Serialization;
using CaseFlow.Api.Auth;
using CaseFlow.Api.ErrorHandling;
using CaseFlow.Api.OpenApi;
using CaseFlow.Api.RateLimiting;
using CaseFlow.Api.Security;
using CaseFlow.Application;
using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases;
using CaseFlow.Application.Demo;
using CaseFlow.Infrastructure;
using CaseFlow.Infrastructure.Demo;
using CaseFlow.Infrastructure.Persistence;
using CaseFlow.Infrastructure.Scheduling;
using CaseFlow.Infrastructure.Scheduling.Hangfire;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog is the application logger. Structured by default; the request-logging
// middleware below enriches each completed request with caller and trace
// context.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        // Enums travel as their names ("PendingReview"), not bare integers.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddCaseFlowOpenApi();
builder.Services.AddCaseFlowRateLimiting(builder.Configuration);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Hangfire client + dashboard storage. The API enqueues delayed jobs (the
// auto-archive on approval) and serves the dashboard; the worker runs the
// server that actually executes them.
builder.Services.AddCaseFlowHangfire();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<DemoTokenService>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<WorkflowOptions>(builder.Configuration.GetSection(WorkflowOptions.SectionName));
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.SectionName));

// In the public demo, cap the request body so a visitor cannot post a huge
// payload at the sandbox. Generous for the small JSON this API accepts.
if (builder.Configuration.GetValue<bool>("Demo:Enabled"))
{
    builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 64 * 1024);
}

// Traces and metrics for the HTTP surface and outbound calls. Exporting is
// opt-in: an OTLP endpoint is wired only when configured, so a demo without a
// collector stays quiet. Swap UseOtlpExporter for a vendor exporter
// (Prometheus, Jaeger, Grafana, Azure Monitor, Datadog) as needed.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("CaseFlow.Api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation());

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    builder.Services.AddOpenTelemetry().UseOtlpExporter();
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

// Bearer options are configured through the options pipeline rather than
// inline, so configuration is read when the options are first resolved -
// not captured at startup. That keeps integration test config overrides
// honest and is the same reason the connection string is read lazily.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured with at least 32 bytes. Use the Jwt__SigningKey environment variable in production.");
        }

        // Keep the raw JWT claim names ("sub", "org", "role") instead of the
        // legacy SOAP-era URIs the handler maps by default.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization(options => options.AddCaseFlowPolicies());

builder.Services.AddHealthChecks()
    .AddNpgSql(
        sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("CaseFlow")
            ?? throw new InvalidOperationException("Connection string 'CaseFlow' is not configured."),
        name: "postgres",
        tags: ["ready"]);

var app = builder.Build();

var demoEnabled = app.Configuration.GetValue<bool>("Demo:Enabled");

// Behind the reverse proxy that terminates TLS, honor X-Forwarded-* so scheme
// and client IP are correct - rate limiting partitions by real IP and links
// render as https. The container is only reachable through that proxy, so the
// forwarders are trusted (KnownProxies/Networks cleared).
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeaders.KnownIPNetworks.Clear();
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCaseFlowSecurityHeaders();

// Logs one structured line per request with method, path, status, and elapsed
// time, plus the caller and trace identifiers pulled from the token/activity.
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnostic, httpContext) =>
    {
        diagnostic.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
        diagnostic.Set("OrganizationId", httpContext.User.FindFirst("org")?.Value);
        diagnostic.Set("TraceId", System.Diagnostics.Activity.Current?.TraceId.ToString());
    };
});

// OpenAPI document and Swagger UI: always in development, and on the public
// demo so reviewers can exercise the API live. Swagger UI is the demo's front
// door at /swagger.
if (app.Environment.IsDevelopment() || demoEnabled)
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "CaseFlow API v1");
        options.DocumentTitle = "CaseFlow API";
    });
}

// Apply migrations on startup in development, and when explicitly opted in -
// the demo deployment sets Database:MigrateOnStartup against its throwaway
// database. A real production environment runs migrations as a deploy step.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<CaseFlowDbContext>().Database.Migrate();
}

// Seed the sandbox on first run in demo mode (idempotent - skips if data
// exists; the scheduled reset job keeps it fresh thereafter).
if (demoEnabled)
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedAsync();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

// Hangfire dashboard. Full access in local development; publicly reachable but
// read-only in demo mode; locked everywhere else.
var dashboardEnabled = app.Environment.IsDevelopment() || demoEnabled;
app.UseHangfireDashboard("/jobs", new DashboardOptions
{
    DashboardTitle = "CaseFlow Jobs",
    Authorization = [new HangfireDashboardAuthorizationFilter(dashboardEnabled)],
    IsReadOnlyFunc = _ => !app.Environment.IsDevelopment(),
});

// Liveness answers "is the process up", readiness answers "can we actually serve
// traffic". Readiness includes the database check so a reverse proxy or orchestrator
// holds traffic until Postgres is reachable instead of serving 500s during startup.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready") });

// Rate limiting applies to the business API only - health and the dashboard
// above are deliberately left out so probes and operators are never throttled.
app.MapControllers().RequireRateLimiting(RateLimitingExtensions.PerCallerPolicy);

app.Run();

// WebApplicationFactory needs a visible entry point to hook integration tests onto.
public partial class Program;

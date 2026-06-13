using Microsoft.OpenApi;

namespace CaseFlow.Api.OpenApi;

// Enriches the generated OpenAPI document: real title/description/version and a
// Bearer security scheme so the Swagger UI (enabled for the hosted demo in
// phase 7) shows an Authorize button wired to the demo token endpoint.
public static class OpenApiConfiguration
{
    public static IServiceCollection AddCaseFlowOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "CaseFlow API",
                    Version = "v1",
                    Description =
                        "Production-style case intake and approval workflow API. "
                        + "Demonstrates clean architecture, workflow state transitions, "
                        + "policy and object-level authorization, the outbox pattern, "
                        + "idempotency keys, and optimistic concurrency. "
                        + "Grab a role-scoped token from POST /api/v1/demo/token and authorize.",
                };

                var bearerScheme = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "JWT bearer token from POST /api/v1/demo/token.",
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = bearerScheme;

                return Task.CompletedTask;
            });
        });

        return services;
    }
}

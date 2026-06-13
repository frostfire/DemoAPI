namespace CaseFlow.Api.Security;

// A small set of response hardening headers applied to every response. HSTS is
// left to the reverse proxy (it owns TLS), and the CSP is intentionally
// permissive enough for Swagger UI's inline assets to load - tightened at the
// proxy if needed. See docs/demo-hosting.md.
public static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseCaseFlowSecurityHeaders(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "SAMEORIGIN";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy =
                "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'";

            await next();
        });
    }
}

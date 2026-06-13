using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace CaseFlow.IntegrationTests;

// Boots the real API against a throwaway Postgres container. Nothing is
// mocked: real JWT validation, real EF Core, real migrations, real SQL. One
// container per test class collection keeps the suite honest and still fast.
public sealed class CaseFlowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    // Explicit implementation: xUnit's DisposeAsync returns Task while the
    // factory's own returns ValueTask, and both need to run.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development environment so startup migrations run against the
        // container - same code path a developer exercises locally.
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CaseFlow"] = _postgres.GetConnectionString(),
                ["Jwt:SigningKey"] = "integration-test-signing-key-not-used-anywhere-real",
                ["DemoAuth:Enabled"] = "true",
                // Lift the rate limits well clear of the suite: many tests issue
                // demo tokens from the same (anonymous) partition in quick
                // succession, which would otherwise trip the default ceiling.
                ["RateLimiting:AnonymousPermitPerMinute"] = "100000",
                ["RateLimiting:AuthenticatedPermitPerMinute"] = "100000",
                ["RateLimiting:AdminPermitPerMinute"] = "100000",
            });
        });
    }
}

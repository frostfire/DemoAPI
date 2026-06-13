using CaseFlow.Application.Abstractions;
using CaseFlow.Infrastructure.Idempotency;
using CaseFlow.Infrastructure.Notifications;
using CaseFlow.Infrastructure.Outbox;
using CaseFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CaseFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Registered with TryAdd so the worker (which does not call
        // AddApplication) gets a clock too.
        services.TryAddSingleton(TimeProvider.System);

        // Connection string is resolved when the context is constructed, not
        // captured at registration - late-bound config (tests, reloads) stays
        // in effect.
        services.AddDbContext<CaseFlowDbContext>((sp, options) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("CaseFlow")
                ?? throw new InvalidOperationException(
                    "Connection string 'CaseFlow' is not configured. Set it in appsettings or the ConnectionStrings__CaseFlow environment variable.");
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<ICaseRepository, CaseRepository>();
        services.AddScoped<ICaseSearchQueries, CaseSearchQueries>();
        services.AddScoped<ICaseAuditQueries, CaseAuditQueries>();
        services.AddScoped<IIdempotencyStore, EfIdempotencyStore>();
        services.AddScoped<INotificationSender, FakeNotificationSender>();
        services.AddScoped<OutboxBatchProcessor>();
        services.AddScoped<Demo.DemoDataSeeder>();

        return services;
    }
}

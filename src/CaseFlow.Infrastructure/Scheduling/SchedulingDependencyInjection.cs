using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Demo;
using CaseFlow.Infrastructure.Maintenance;
using CaseFlow.Infrastructure.Scheduling.Hangfire;
using CaseFlow.Infrastructure.Scheduling.Quartz;
using global::Hangfire;
using global::Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace CaseFlow.Infrastructure.Scheduling;

// This repo deliberately runs two schedulers side by side. A real project
// would pick one - the point here is to show the judgment about which kind of
// job fits which tool. The reasoning is in docs/decisions/0005, and the wider
// "implement vs comment" convention is in docs/tooling-choices.md.
//
// Scheduling alternatives worth knowing as project needs change:
//   - TickerQ:          source-generated, EF Core-backed, ships its own dashboard
//   - Coravel:          zero-infrastructure in-process scheduling, great for simple invocables
//   - FluentScheduler:  minimal fluent in-process scheduler
//   - Didact:           full .NET job orchestration platform with a UI
// Quartz.NET fits cron-driven, calendar-aware scheduling (work identified by
// when it runs). Hangfire fits persistent queued/delayed/retried work with
// dashboard visibility (work identified by what it does).
public static class SchedulingDependencyInjection
{
    // Hangfire client + storage. Registered in both the API (which enqueues
    // the auto-archive job and serves the dashboard) and the worker (which
    // runs the server), so both share one storage in the CaseFlow database.
    //
    // The connection string is resolved from the service provider, not read at
    // registration: the storage is built after the host is fully composed, so
    // it sees the final merged configuration (the same reason the DbContext and
    // JWT options resolve lazily). Reading it eagerly here would capture
    // whatever appsettings said before later sources - test overrides, env
    // vars - were applied.
    public static IServiceCollection AddCaseFlowHangfire(this IServiceCollection services)
    {
        services.AddHangfire((provider, config) =>
        {
            var connectionString = provider.GetRequiredService<IConfiguration>().GetConnectionString("CaseFlow")
                ?? throw new InvalidOperationException("Connection string 'CaseFlow' is not configured.");

            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                // Hangfire keeps its own tables in a separate "hangfire" schema
                // in the same database, and creates them on first use.
                .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connectionString));
        });

        services.AddScoped<OutboxDispatchJob>();
        services.AddScoped<CaseAutoArchiveJob>();
        services.AddScoped<IJobScheduler, HangfireJobScheduler>();

        return services;
    }

    // Hangfire server - worker only. Processes the queued/delayed/recurring
    // jobs and registers the recurring outbox drain on startup.
    public static IServiceCollection AddCaseFlowHangfireServer(this IServiceCollection services)
    {
        services.AddHangfireServer();
        services.AddHostedService<RecurringJobsRegistrar>();
        return services;
    }

    // Quartz host - worker only. Cron-driven maintenance jobs, plus the demo
    // sandbox reset when demo mode is on.
    public static IServiceCollection AddCaseFlowQuartz(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<CaseMaintenanceService>();

        var demo = configuration.GetSection(DemoOptions.SectionName).Get<DemoOptions>() ?? new DemoOptions();

        services.AddQuartz(q =>
        {
            var staleDrafts = new JobKey("expire-stale-drafts");
            q.AddJob<ExpireStaleDraftsJob>(staleDrafts, j => j.WithDescription("Delete abandoned draft cases"));
            q.AddTrigger(t => t
                .ForJob(staleDrafts)
                .WithIdentity("expire-stale-drafts-hourly")
                .WithCronSchedule("0 0 * * * ?")); // top of every hour

            var idempotencyCleanup = new JobKey("cleanup-idempotency-records");
            q.AddJob<CleanupIdempotencyRecordsJob>(idempotencyCleanup, j => j.WithDescription("Delete expired idempotency records"));
            q.AddTrigger(t => t
                .ForJob(idempotencyCleanup)
                .WithIdentity("cleanup-idempotency-nightly")
                .WithCronSchedule("0 0 3 * * ?")); // 03:00 daily

            if (demo.Enabled)
            {
                // Simple interval rather than cron: the cadence is configurable
                // and "every N hours from startup" reads more clearly here.
                var demoReset = new JobKey("demo-data-reset");
                q.AddJob<DemoDataResetJob>(demoReset, j => j.WithDescription("Reset the public demo sandbox to seed state"));
                q.AddTrigger(t => t
                    .ForJob(demoReset)
                    .WithIdentity("demo-data-reset-interval")
                    .StartAt(DateBuilder.FutureDate((int)demo.ResetEvery.TotalMinutes, IntervalUnit.Minute))
                    .WithSimpleSchedule(s => s.WithInterval(demo.ResetEvery).RepeatForever()));
            }

            // For a single worker the default in-memory job store is right. A
            // multi-instance deployment would switch to the ADO.NET store with
            // clustering enabled so a job fires on exactly one node:
            //   q.UsePersistentStore(s => { s.UsePostgres(connectionString); s.UseClustering(); });
        });

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        return services;
    }
}

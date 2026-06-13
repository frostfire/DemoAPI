# Tooling choices

A feature-rich API can quietly turn into a library zoo. CaseFlow follows one
convention to stay rich without diluting:

1. **Implement** a tool when it genuinely serves the domain.
2. **Comment** a short "here's where this would grow, and why it isn't needed
   yet" note at the natural extension point when it does not.
3. **Collect** those notes here so the breadth is discoverable in one place.

The rule that keeps this honest: a comment must say *where a tool would slot in
and why we didn't need it yet*. A comment that just name-drops a library is
noise; one that explains the decision is a design artifact.

## Implemented

| Tool | Used for | Alternatives considered |
|---|---|---|
| EF Core + Npgsql | Writes, migrations, the outbox transaction | Dapper everywhere; EF earns its keep on the write side |
| Dapper | The one case-search query | EF projection; raw SQL is shorter and EXPLAIN-able for dynamic sort + paging |
| FluentValidation | Application-boundary validation | DataAnnotations / MiniValidation for smaller surfaces |
| Mapperly | Contract mapping (compile-time, source-generated) | AutoMapper, Mapster, or hand-written |
| ASP.NET Core JWT bearer | Authentication | Any OIDC provider; the demo token endpoint stands in for one |
| Quartz.NET | Cron maintenance jobs | See [ADR 0005](decisions/0005-dual-scheduler-showcase.md) |
| Hangfire | Outbox drain, delayed auto-archive, dashboard | TickerQ, Coravel, FluentScheduler, Didact |
| Postgres `xmin` | Optimistic concurrency / ETags | A dedicated rowversion column + trigger |
| Testcontainers | Integration tests against real Postgres | In-memory provider (doesn't catch SQL-level behaviour) |

## Comment-only (mentioned at the extension point, not installed)

These are noted in the code where they would naturally slot in:

- **Messaging**: MassTransit / Wolverine / Rebus / CAP, where the outbox would
  publish if this ever grew into cross-service integration.
- **Real-time**: SignalR, for live case-status updates to a dashboard.
- **Feature flags**: Microsoft.FeatureManagement (or OpenFeature, LaunchDarkly,
  ConfigCat).
- **Typed clients**: Refit or Kiota for consuming the API.
- **Search**: PostgreSQL full-text (`tsvector`) before reaching for Elasticsearch.
- **Read models**: GraphQL (HotChocolate) or gRPC for internal consumers.
- **Performance**: BenchmarkDotNet, plus load testing with k6 or NBomber.
- **Test depth**: mutation testing with Stryker.NET.
- **Orchestration**: .NET Aspire, as an alternative to docker-compose for local
  multi-service runs.

## Scheduling alternatives (the dual-scheduler note, in full)

Mirrored from the registration point in `SchedulingDependencyInjection`:

- **TickerQ**: source-generated, EF Core-backed, ships its own dashboard.
- **Coravel**: zero-infrastructure in-process scheduling; great for simple
  invocables.
- **FluentScheduler**: minimal fluent in-process scheduler.
- **Didact**: a full .NET job orchestration platform with a UI.

Quartz.NET fits cron-driven, calendar-aware scheduling (work identified by when
it runs). Hangfire fits persistent queued/delayed/retried work with dashboard
visibility (work identified by what it does).

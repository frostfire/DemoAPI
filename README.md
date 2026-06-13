# CaseFlow API

[![Build and Test](https://github.com/frostfire/DemoAPI/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/frostfire/DemoAPI/actions/workflows/build-and-test.yml)

A production-style .NET 10 Web API that models a case intake and approval workflow.

It's a public demo project, so the point isn't really the domain. It's showing how a real business API gets put together: clear boundaries, reliable workflows, consistent contracts, honest error handling, and deployment patterns that actually hold up.

> **Live demo:** [demoapi.davidborneman.com/swagger](https://demoapi.davidborneman.com/swagger), a sandboxed instance you can drive from the browser.
>
> Grab a role-scoped token from `POST /api/v1/demo/token` (try `["CaseWriter"]` or `["CaseApprover"]`), click **Authorize**, and run the workflow. The background jobs that result show up in the read-only [jobs dashboard](https://demoapi.davidborneman.com/jobs). The sandbox reseeds itself periodically; `GET /api/v1/demo/info` spells out the rules.

## Why this exists

Most API samples stop at CRUD, and CRUD hides all the interesting problems. CaseFlow models an actual workflow instead: users create cases, submit them for review, reviewers approve or reject, and the system keeps a full audit and event history behind it.

That domain forces the design questions you hit in real production systems:

- What happens when someone approves a case that was never submitted? (409, with a ProblemDetails body that explains itself.)
- What stops a reviewer from approving their own case? (Policy plus object-level authorization, with tests.)
- What happens when a submit request gets retried? (Idempotency keys: same response, no duplicate events.)
- Two people editing the same case at once? Optimistic concurrency, ETag / If-Match, 412.
- And the classic one: how does "send a notification" survive a crash between the database write and the send? Transactional outbox plus a background worker.

## What it demonstrates

- **Clean architecture**, with the boundaries enforced by architecture tests rather than just good intentions
- A **workflow state machine** with explicit valid transitions and meaningful conflict responses
- **Contract separation.** The public request/response models are not the domain entities
- **Validation and errors:** FluentValidation at the application boundary, RFC 7807 ProblemDetails everywhere else
- **Security:** JWT bearer auth, policy-based authorization, and object-level rules (a user in Organization A cannot read Organization B's cases)
- **Reliability patterns:** transactional outbox, idempotency keys, optimistic concurrency
- **Scheduling.** Quartz.NET for cron-driven jobs, Hangfire for queued/delayed/retried work, side by side on purpose, with notes on where TickerQ, Coravel, Didact, or FluentScheduler would fit instead
- **Testing that means something:** unit tests on the domain rules, Testcontainers-based integration tests against real PostgreSQL, architecture tests on the boundaries, plus authorization tests
- **Operations:** health checks, structured logging, OpenTelemetry, rate limiting, Docker Compose for local dev, GitHub Actions CI

## Tech stack

.NET 10, ASP.NET Core (controllers), EF Core + PostgreSQL 16 (with one deliberate Dapper query where raw SQL earns its keep), Quartz.NET, Hangfire, FluentValidation, Serilog, OpenTelemetry, xUnit, Testcontainers, Docker Compose, GitHub Actions.

Where a good library was *considered* but not actually needed yet, there's a short note at the natural extension point explaining the tradeoff. Those are collected in [`docs/tooling-choices.md`](docs/tooling-choices.md). A demo repo should show judgment, not just a long dependency list.

## What's here

The API covers the full case lifecycle, and the supporting pieces are all wired up:

- Case workflow: create / search / get, submit / approve / reject, validation, ProblemDetails
- JWT auth, authorization policies, object-level access control
- Outbox, domain events, a worker service, idempotency, optimistic concurrency
- Quartz.NET + Hangfire scheduling
- OpenAPI examples, rate limiting, observability, CI, architecture tests
- A public hosted sandbox (demo mode, role-scoped tokens, periodic data reset), [live here](https://demoapi.davidborneman.com/swagger)

Design decisions live in [`docs/decisions`](docs/decisions) as ADRs if you want the reasoning behind any of it.

## Running locally

Start the local database, then the API and worker:

```bash
docker compose up -d                       # PostgreSQL on localhost:5433
dotnet run --project src/CaseFlow.Api      # API (migrations apply on startup in Development)
dotnet run --project src/CaseFlow.Worker   # outbox dispatch + scheduled jobs
```

The API serves OpenAPI at `/openapi/v1.json`, and the Hangfire dashboard at `/jobs` in Development.

Run the full test suite (unit, architecture, and the Testcontainers-backed integration tests, so Docker is required):

```bash
dotnet test
```

## Running the whole stack in containers

`docker-compose.prod.yml` builds the API and worker images and runs them alongside PostgreSQL, the same way the hosted demo runs. Copy `.env.prod.sample` to `.env`, set the secrets, then:

```bash
docker compose -f docker-compose.prod.yml up -d --build
```

## License

[MIT](LICENSE)

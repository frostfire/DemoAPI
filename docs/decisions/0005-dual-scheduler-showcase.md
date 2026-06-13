# 0005: Run Quartz.NET and Hangfire side by side, on purpose

## Status

Accepted

## Context

CaseFlow needs background scheduling for two genuinely different kinds of work:

- **Time-driven maintenance**: "expire abandoned drafts every hour", "prune
  expired idempotency records nightly". The identity of this work is *a
  schedule*. Nothing enqueues it; it just fires when the clock says so.
- **Work-driven jobs**: "drain the outbox", "archive this specific case in N
  days". The identity of this work is *a unit of work*. Something produces it,
  it needs durable storage, retries, and ideally a dashboard to see what ran.

A single library can be bent to cover both, and in a real project that is
usually the right call - one scheduler is one thing to operate. This repository
is a portfolio piece, so it makes the split explicit to demonstrate the
judgment about which tool fits which job shape.

## Decision

Use **both**, each for the work it is actually best at:

- **Quartz.NET** (hosted in the worker) runs the cron maintenance jobs. Cron
  triggers with `[DisallowConcurrentExecution]`, an in-memory job store, and a
  commented-out ADO/clustered store showing what a multi-instance deployment
  would change.
- **Hangfire** (server in the worker, dashboard mounted in the API, storage in
  a separate schema of the same PostgreSQL database) runs the recurring outbox
  drain and the delayed per-case auto-archive job. The dashboard is the visible
  payoff: every run, every retry, every scheduled job is inspectable.

The application layer depends only on `IJobScheduler`; the Hangfire binding
lives entirely in Infrastructure, so the choice is swappable and the
architecture tests stay green.

## Consequences

- The repo demonstrates two of the dominant .NET scheduling libraries and,
  more importantly, *why* you would reach for each.
- There are two schedulers to understand. This ADR and the registration-point
  comments exist so that reads as a deliberate decision, not indecision.
- A real single-scheduler project could collapse this: Hangfire can run the
  cron jobs too, or Quartz with a persistent store can own the durable work.
  The registration points note the alternatives (TickerQ, Coravel,
  FluentScheduler, Didact) for when project needs change.

## Notes

The phase 4 outbox poll loop (a hand-rolled `BackgroundService`) was replaced
by the Hangfire recurring job in this phase - same `OutboxBatchProcessor`, now
with dashboard visibility and managed scheduling. That migration is itself part
of the point: the reliable-delivery logic never moved, only the thing that
drives it.

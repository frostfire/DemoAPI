# 0002: Use the transactional outbox pattern

## Status

Accepted

## Context

Approving or rejecting a case must update the case, record an audit entry, and
trigger a notification. If the notification is sent inline, a crash between the
database commit and the send leaves the system inconsistent - or a rollback
after a send produces a notification for something that never happened.

## Decision

Each workflow transition raises a domain event. The DbContext `SaveChanges`
override writes an audit entry and an outbox message for every event in the
same transaction as the state change. A background processor later reads
unprocessed messages, dispatches them, and marks them done, with exponential
backoff on failure. Delivery is at-least-once; consumers must tolerate
duplicates.

## Consequences

- The state change and its side-effect intent commit atomically; delivery is
  decoupled and retryable.
- A message can be delivered more than once, so the (fake) notification sender
  and any future consumer must be idempotent.
- Single-instance dispatch is assumed; multiple workers would need
  `FOR UPDATE SKIP LOCKED` on the poll query. See
  [0005](0005-dual-scheduler-showcase.md) for how the dispatch loop moved onto
  Hangfire.

# 0007: Use controllers rather than minimal APIs

## Status

Accepted

## Context

Both controllers and grouped minimal APIs are good fits for this surface. The
deciding factor is that this is a portfolio repo: a reviewer should be able to
navigate it quickly.

## Decision

Use attribute-routed controllers, organized by resource area (Cases, Workflow,
Audit, Demo). Keep them thin - each action maps a request to a command or
query, dispatches it through an application handler, and maps the result back to
a contract. No business logic in controllers.

## Consequences

- Familiar, greppable structure; the endpoint-to-handler path is one F12.
- Slightly more ceremony than minimal API endpoint groups, accepted for
  readability. A larger system might prefer grouped minimal APIs or vertical
  slices; the application layer would be unchanged either way.

# 0003: Use ProblemDetails for error responses

## Status

Accepted

## Context

An API consumed by other developers needs one predictable error shape, not a
different ad-hoc body per failure. ASP.NET Core ships first-class support for
RFC 7807 ProblemDetails.

## Decision

All non-success responses use ProblemDetails. A single
`GlobalExceptionHandler` maps exception types to status codes in one place:
validation failures to 400 (with a field-error dictionary), missing resources
to 404, access and self-approval violations to 403, invalid workflow
transitions to 409, concurrency conflicts to 412, and other domain rule
violations to 422. Unrecognized exceptions fall through to the framework's
default 500 so internal details never leak.

## Consequences

- Consumers parse one shape, and every error carries a trace id for
  correlation.
- New failure modes are wired by adding one arm to the mapping switch, not by
  inventing a new response body at the call site.

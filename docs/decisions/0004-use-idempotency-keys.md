# 0004: Use idempotency keys for workflow commands

## Status

Accepted

## Context

Network retries and impatient clicks can submit the same workflow command
twice. Without protection, a retried submit/approve/reject either errors on the
second attempt (the case already moved) or, worse, double-runs a side effect.

## Decision

Workflow command endpoints accept an optional `Idempotency-Key` header. The
first successful response is stored keyed by the header value together with a
hash of the request. A retry with the same key and the same request replays the
stored response verbatim, without re-executing. The same key with a different
request is a client error (422). Stored responses expire and are pruned by a
scheduled cleanup job.

## Consequences

- Safe client retries: the same key never runs the command twice.
- Idempotency is opt-in per request; callers that do not send the header get
  ordinary behaviour.
- Only successful responses are stored, so a failed attempt can still be
  retried and actually execute.

# 0001: Use clean architecture

## Status

Accepted

## Context

The repo is meant to demonstrate maintainable API design with clear dependency
boundaries, not a minimal sample. The boundaries need to be obvious to a
reviewer and enforceable, not just conventional.

## Decision

Split into Domain, Application, Infrastructure, Contracts, Api, and Worker
projects. Dependencies point inward: Domain depends on nothing, Application on
Domain, Infrastructure on Application and Domain, Api/Worker compose the rest.
Public wire contracts live in their own project so they never couple to domain
internals. The rules are enforced by `CaseFlow.ArchitectureTests`, not just by
project references.

## Consequences

- More projects than a single-assembly sample, but each boundary is explicit
  and testable, and a violation fails a test rather than rotting quietly.
- The composition roots (Api, Worker) are the only places that know how the
  pieces fit together.

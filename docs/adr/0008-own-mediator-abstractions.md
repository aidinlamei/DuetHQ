# 0008. Own mediator abstractions; third-party mediator optional

## Status

Accepted

## Context

The application layer uses CQRS with one handler per command or query, and cross-cutting concerns that matter for privacy and correctness (validation, transaction, tenant context, logging redaction) need a single, consistent place to run. A common way to do this is a mediator library, but application code that depends directly on a third-party mediator becomes coupled to its API, its behavior and its licensing terms. `docs/ARCHITECTURE.md` §3.1 already allows a third-party mediator only behind our own abstractions and only if its license terms are acceptable.

## Decision

- Define our own abstractions in `DuetHQ.Application.Abstractions`: `ICommand`, `ICommand<T>`, `IQuery<T>`, `ICommandHandler<TCommand>`, `IQueryHandler<TQuery, TResult>`, and a dispatcher interface.
- **Handlers depend only on these abstractions**, never directly on a third-party mediator (`CLAUDE.md` §3.3). Handlers are thin: orchestrate domain objects and ports.
- Commands return `Result` / `Result<TId>`; expected failures (validation, not found, forbidden, conflict) are results, not exceptions. Queries never modify state and return DTOs from read models.
- **Pipeline behaviors** implement cross-cutting concerns: validation, transaction (one per command per module), tenant context, and logging redaction. They live outside handlers.
- The default is a small in-process dispatcher. A third-party mediator such as MediatR may be plugged in **behind these abstractions** if and only if its license terms are acceptable; switching must not change any handler.

## Consequences

- Application code has no dependency on a specific mediator package; swapping or removing one is a change confined to infrastructure/composition.
- We own a small amount of dispatcher and pipeline code and its tests (P2-02: a sample command flowing through a validation behavior).
- Behaviors give one enforcement point for privacy-relevant cross-cutting rules, such as never logging `/checkin/*` request bodies or `[SensitiveData]` types.
- Handler registration and pipeline ordering must be explicit and tested, since a misordered pipeline (for example, transaction before tenant context) can break RLS.
- Integration events are a separate mechanism (transactional outbox with an in-process dispatcher, per module) and do not go through this mediator abstraction.

## Alternatives considered

- **Use MediatR directly in handlers.** Rejected: couples every handler to one library and to its licensing terms.
- **No dispatcher; inject handlers directly and call them.** Rejected as the default: every call site would have to compose validation, transaction and tenant context manually, which is easy to get wrong in privacy-critical paths.
- **Other third-party messaging or workflow frameworks.** Rejected for the MVP: more surface area and configuration than the pilot needs, and no advantage over a small owned dispatcher behind our abstractions.

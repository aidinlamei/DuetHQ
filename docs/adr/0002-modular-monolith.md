# 0002. Modular monolith with Clean Architecture per module

## Status

Accepted

## Context

DuetHQ starts as free pilots with several companies, run by a small team. The product's core promise is privacy: anonymity must come from how data is separated, not from UI conventions (`docs/ARCHITECTURE.md` §3). The architecture therefore needs hard, checkable boundaries between the parts that hold identity (`vault`, `checkin`) and the parts that hold anonymous aggregates (`pool`, `insights`), while staying cheap to build, deploy and operate for a pilot.

The MVP must also not require a rewrite if a module is later extracted.

## Decision

- **One deployable ASP.NET Core host** (`DuetHQ.Web`) containing eight modules that map to bounded contexts: Organization, Content, CheckIn, Personal, Insights, Actions, Notifications, Pilot.
- **Modules reference each other only through `<Module>.Contracts`** (sync query interfaces, DTOs, integration events). Module internals are `internal`. No cross-module schema access and no cross-schema foreign keys; other modules are referenced by strongly typed IDs only.
- **One PostgreSQL database, one schema per module**, one EF Core `DbContext` per module, each connecting with its own least-privilege database role (§10).
- **Clean Architecture inside each module**: `Domain` → `Application` → `Infrastructure`, with `Endpoints` mapping HTTP to commands/queries. Dependencies point inward only.
- **DDD tactical patterns and CQRS**: aggregates, value objects, strongly typed IDs, domain events; commands mutate aggregates, queries read from no-tracking read models.
- **Boundaries are enforced by architecture tests** (NetArchTest, task P1-01), e.g. `DuetHQ.Modules.Insights` has no reference to `DuetHQ.Modules.Personal`; `Domain` has no dependency on `Application`, `Infrastructure`, EF Core or ASP.NET Core.

## Consequences

- One build, one deploy, one database to operate during the pilot; in-process calls are cheap and transactions are local to a module.
- The privacy separation is visible in the project graph and the database roles, so a violation fails a test instead of relying on review.
- Each module has its own migrations and migration history table in its schema; tenant-scoped tables carry `tenant_id` and an RLS policy.
- The cost is discipline and ceremony: a `.Contracts` project per module, mapping at the boundaries, and architecture tests that must be kept green and extended.
- Because cross-module coupling is limited to Contracts, a module can later be extracted into its own service without changing its callers' shape. That is a possibility this ADR keeps open, not a plan.

## Alternatives considered

- **Microservices.** Rejected: operational cost and distributed-transaction complexity are unjustified for a pilot, and they do not by themselves give a stronger privacy guarantee than schema/role separation plus enforced boundaries.
- **Single layered monolith without module boundaries.** Rejected: nothing would stop a query from joining identity and emotional data, and INV-01 would depend on reviewer vigilance.
- **Modules with shared database access (cross-schema joins/FKs).** Rejected: it defeats the per-module role model in §10 and makes the `vault`/`pool` separation unenforceable.
- **Event sourcing, a separate read database, generic repositories, service locator.** Rejected as unnecessary for MVP scope (`CLAUDE.md` §4); read models live in the module that serves them.

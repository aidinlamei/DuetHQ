# CLAUDE.md — DuetHQ

DuetHQ is a multi-tenant SaaS where employees answer a short character-led check-in and leads/HR see only anonymous, aggregated team insights. Privacy is the product. Read this file fully before every task.

The architecture reference is `docs/ARCHITECTURE.md`. Before changing anything, read the sections relevant to the task (at minimum §5 Modules and §6 Privacy invariants).

---

## 0. Priority order when rules conflict

1. Privacy invariants (INV-01 … INV-12 in `docs/ARCHITECTURE.md` §6)
2. Module boundaries and layering
3. Correctness and tests
4. Simplicity and MVP scope
5. Performance and convenience

If a task seems to require breaking a higher rule, **stop and ask**. Never work around it silently.

---

## 1. How to work

- Plan before coding: state which module(s), which layer(s), which files, and which invariants are touched.
- Work in small, reviewable steps. One vertical slice at a time (domain → application → infrastructure → endpoint → UI → tests).
- Build and run tests after every step: `dotnet build -warnaserror` and `dotnet test`.
- Do not add features outside MVP scope (`docs/ARCHITECTURE.md` §1 non-goals, §14 deferred). If something seems needed, propose it; do not build it.
- If an architectural decision changes, write or update an ADR in `docs/adr/NNNN-title.md` and update `docs/ARCHITECTURE.md` in the same change.
- Never change a section marked **INVARIANT** without an ADR and explicit human approval in the conversation.
- When uncertain whether something could leak identity, treat it as a leak and ask.

---

## 2. Privacy rules (non-negotiable)

- Never create a table, DTO, event, log line, cache entry, query or API response that contains both a person identifier (`MemberId`, email, token, IP) and emotional content (answers, reading, state, intensity, need, safety result). The only exception is `vault.personal_entry`, readable only by that member.
- `AnonymousResponse` must never get: timestamp columns, sequence/identity columns, member references, slot keys, or anything finer than `PeriodKey`.
- Never write `Participation`/`PersonalEntry` and `AnonymousResponse` in the same database transaction. Anonymous responses go only through `AnonymousBuffer` and its shuffled batch flusher.
- Never read the `pool` schema outside the Insights aggregator. Panels, exports and endpoints read `CohortSnapshot` only.
- Never expose a snapshot that the suppression policy marked as suppressed, and never compute ad-hoc aggregates that bypass `SuppressionPolicy`.
- Never add filters that combine dimensions (team × role × tenure, etc.). Drill-down follows cohorts only.
- A lead's or executive's own check-in always resolves to the Leadership cohort via `CohortResolver`. Do not special-case this elsewhere.
- Show participation as rates only. Never list who answered or who did not.
- `MinGroupSize` must stay ≥ 5. Do not add configuration that lowers it.
- Safety evaluation lives only in the Personal module and is shown only to the member. No events, logs, notifications or metrics about it.
- Every insights read by a viewer writes an `AccessAuditEntry`. Add it in the query handler, not in the UI.
- `TenantAdmin` gets configuration only. Never add support, impersonation or "debug" access to `vault` or `pool`.
- Check-in collects no free text. No runtime AI or LLM calls anywhere in scoring, suppression, signals or the conversation.
- Mark sensitive types with `[SensitiveData]`. Never log request bodies of `/checkin/*`. Never put payloads in exception messages.

---

## 3. Architecture rules

### 3.1 Modular monolith
- Modules: Organization, Content, CheckIn, Personal, Insights, Actions, Notifications, Pilot.
- A module references other modules **only** through `<Module>.Contracts` (queries, DTOs, integration events).
- Module internals are `internal`. Expose only what Contracts needs.
- No cross-schema foreign keys, joins or queries. Reference other modules by strongly typed IDs.
- Synchronous cross-module calls: Contracts query interfaces. Asynchronous: integration events via the module's outbox.

### 3.2 Clean Architecture inside a module
- `Domain`: entities, aggregates, value objects, domain events, domain services, specifications. Depends on `DuetHQ.SharedKernel` only. No references to EF Core, ASP.NET, logging, or any infrastructure. No async I/O.
- `Application`: commands, queries, handlers, validators, ports (interfaces), DTOs. Depends on Domain, `DuetHQ.SharedKernel`, `DuetHQ.Application.Abstractions` and any module's `.Contracts` (its own included); cross-module calls go straight to a `.Contracts` interface, no adapter needed. Never another module's host assembly, never Infrastructure. No `Microsoft.*`, `Npgsql`, `Serilog` or `System.Data.*` namespaces (ADR-0009).
- `Infrastructure`: EF Core DbContext, configurations, migrations, repositories, port implementations, background services. May also use `DuetHQ.Infrastructure.Common`.
- `Endpoints`: Minimal API endpoint groups mapping HTTP to commands/queries. No business logic. Depends on Application and Contracts (plus ASP.NET Core); never on Domain or Infrastructure.
- Dependencies point inward only. Architecture tests enforce this; keep them green and extend them for new rules. The module root namespace (`<Module>Module`, the DI entry point) and `.Endpoints` are the only exemptions from the banned-namespace rules.
- `.Contracts` projects depend on `DuetHQ.SharedKernel` only and expose plain interfaces, DTOs and integration events (no `IQuery<T>`). Types shared across modules (IDs, `CohortRef`, the integration-event marker) live in `DuetHQ.SharedKernel`.

### 3.3 CQRS
- Commands change state through aggregates and return `Result` / `Result<TId>`.
- Queries never modify state, never load aggregates for tracking, and return DTOs from read models (EF Core `AsNoTracking` or Dapper).
- One handler per command/query. Handlers are thin: orchestrate domain + ports.
- Handlers depend on our abstractions (`ICommandHandler<TCommand>`, `IQueryHandler<TQuery, TResult>`), not directly on a third-party mediator.

---

## 4. Design patterns to use (and where)

| Pattern | Where | Rule |
|---|---|---|
| Aggregate Root | Tenant, Member, Team, ActionCommitment, PersonalEntry, CheckInSchedule | Modify only through methods that protect invariants. No public setters. |
| Value Object | EmotionReading, PeriodKey, SlotKey, CohortRef, TenantCalendar, PrivacyPolicy, Locale | Immutable `record`/`readonly record struct`, validated in factory. |
| Strongly Typed ID | All IDs (`TenantId`, `MemberId`, `TeamId`, …) | Never pass raw `Guid`/`string` IDs across layers. |
| Domain Service (pure) | EmotionScoringService, CohortResolver, SuppressionPolicy, SafetyPatternEvaluator, TenantCalendarService | Pure functions, no I/O, fully unit tested. |
| Specification | Playbook conditions (`DominantState`, `NeedFrequency`, `ParticipationDrop`, `IntensityRise`) | Closed set in code, parameters from data. |
| Strategy / Adapter | `ICheckInChannel` (Email, WebPush) | New channels are new implementations only. |
| Repository | Aggregates only | No generic repository. No `IQueryable` leaking out of Infrastructure. |
| Unit of Work | Per module DbContext | One transaction per command per module. |
| Transactional Outbox | Integration events | Publish only after commit; handlers must be idempotent. |
| Result pattern | Expected failures | Do not throw for validation, not-found, forbidden, conflict. |
| Options pattern | Configuration | Strongly typed, validated on start (`ValidateOnStart`). |
| Pipeline behaviors | Validation, transaction, tenant context, logging redaction | Cross-cutting concerns live here, not in handlers. |

Do **not** introduce: generic repositories, service locator, static mutable state, event sourcing, microservices, a separate read database, AutoMapper-style reflection mapping for domain objects.

---

## 5. C# and .NET conventions

- .NET 10, C# latest, `<Nullable>enable</Nullable>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<ImplicitUsings>enable</ImplicitUsings>`.
- File-scoped namespaces. One public type per file. Namespace mirrors folder.
- Classes `sealed` by default. `internal` by default inside modules.
- `record` for DTOs and value objects. Primary constructors allowed for services.
- Async all the way; every async method takes and forwards `CancellationToken`. No `.Result`, `.Wait()`, `async void`.
- Time only through injected `TimeProvider`. `DateTime.Now`/`UtcNow`/`Today` and `DateTimeOffset.Now`/`UtcNow` are forbidden (compile error via BannedApiAnalyzers; see `BannedSymbols.txt`).
- Period and slot keys only through `TenantCalendarService`. Never compute weeks with `ISOWeek` or UTC directly.
- Naming: `SubmitCheckInCommand`, `SubmitCheckInCommandHandler`, `GetTeamSnapshotQuery`, `TeamSnapshotDto`, `ParticipationRecordedIntegrationEvent`.
- No magic strings for codes; use constants/value objects (`EmotionStateCode`, `NeedCode`).
- Guard clauses at boundaries; domain invariants inside aggregates.
- Keep methods short; prefer clear names over comments. Comments explain *why*, not *what*.

---

## 6. Persistence rules

- PostgreSQL, one schema per module (`org`, `content`, `checkin`, `vault`, `pool`, `insights`, `actions`, `notify`, `pilot`), `snake_case` names.
- Each module has its own DbContext and migrations; migration history table lives in the module schema.
- Every tenant-scoped table has `tenant_id` and an RLS policy. Never disable RLS or use a bypass role in application code.
- Each DbContext connects with its module's database role (see `docs/ARCHITECTURE.md` §10). Never grant a role access to another module's schema to "make it work".
- Application-generated UUIDv4 for `pool` IDs. No `DEFAULT now()` or serial columns in `pool`.
- Migrations are additive where possible. Destructive migrations need an explicit note in the PR description.
- No lazy loading. Explicit includes or projections.

---

## 7. API and UI rules

- Minimal API endpoint groups per module, versioned under `/api/v1`. Return RFC 7807 problem details with generic messages.
- Authorization via named policies and requirements (e.g. `CanViewCohort`), checked in handlers for data access, not only on endpoints.
- Rate limit `/checkin/submit` and sign-in endpoints.
- Blazor: check-in client is WebAssembly and keeps the dialog state in memory until submit. Panels are Interactive Server.
- All user-visible text uses localization keys. Support `fa` (RTL) and `en`. Test layouts in both directions.
- Accessibility: keyboard navigable, visible focus, sufficient contrast, labels on all controls.
- Character copy rules: ask, never infer ("How are you right now?", never "You seem stressed"). Calm, brief, peer-level. "Not now" always available. No jokes after low-pleasantness results.
- The character, names and visuals are original. Never use Disney/Pixar "Inside Out" names, designs or likeness, or Microsoft Clippy's design.
- Dashboards show distributions, dominant need, participation rate, signals and actions. No per-person views, no leaderboards, no comparison of individuals.

---

## 8. Testing rules and Definition of Done

A task is done only when:

- [ ] Code builds with zero warnings.
- [ ] Unit tests cover new domain logic, including edge cases (ties, empty input, boundaries).
- [ ] Pure domain services (scoring, cohort, suppression, safety, calendar) have property-based tests where applicable.
- [ ] Application handlers have tests for success and each expected failure.
- [ ] Integration tests (Testcontainers PostgreSQL) cover any new table: RLS isolation between two tenants and role permissions.
- [ ] Architecture tests pass; new boundaries or rules are added to them.
- [ ] If the change touches check-in, a privacy regression test asserts that no answers/readings/needs appear in captured logs and that `pool` rows have no timestamps.
- [ ] Localization keys exist for `fa` and `en`.
- [ ] `docs/ARCHITECTURE.md` and ADRs are updated if structure or decisions changed.

Test naming: `MethodOrScenario_Condition_ExpectedResult`. Use Shouldly for assertions. No test depends on wall-clock time; use `FakeTimeProvider`.

Tests must use `FakeTimeProvider`. If a test ever genuinely needs wall-clock time, it uses `TimeProvider.System` explicitly, never `DateTime.UtcNow` (the banned-API analyzer applies to test projects on purpose).

---

## 9. Forbidden (quick checklist)

- ❌ `MemberId` + emotional data in the same object outside `vault`
- ❌ Timestamps, sequences or slot keys in `pool`
- ❌ Same transaction for identity records and anonymous responses
- ❌ Reading `pool` from anywhere except the Insights aggregator
- ❌ Bypassing `SuppressionPolicy` or lowering `MinGroupSize` below 5
- ❌ Showing who did or did not participate
- ❌ Safety results leaving the Personal module
- ❌ Free-text input in check-ins; runtime AI/LLM calls
- ❌ Logging request bodies of `/checkin/*` or sensitive types
- ❌ Cross-module schema access, cross-schema FKs, referencing another module's internals
- ❌ `DateTime.Now`/`UtcNow`, `.Result`, `async void`, generic repositories, static mutable state
- ❌ Features from the deferred list without an approved proposal
- ❌ Third-party copyrighted characters or designs

---

## 10. Commands

```bash
dotnet build -warnaserror
dotnet test
dotnet test tests/DuetHQ.ArchitectureTests
dotnet ef migrations add <Name> --project src/Modules/<Module>/DuetHQ.Modules.<Module> --context <Module>DbContext --output-dir Infrastructure/Persistence/Migrations
dotnet run --project src/DuetHQ.Web
```

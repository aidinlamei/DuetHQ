# DuetHQ — Implementation Plan

This plan turns `docs/ARCHITECTURE.md` into ordered, reviewable tasks for Claude Code.
Rules in `CLAUDE.md` apply to every task.

---

## How to execute this plan

1. Work **one task at a time**, in order. Do not start a task whose dependencies are not done.
2. For each task:
   - Read the task, its "Refs" sections in `docs/ARCHITECTURE.md`, and `CLAUDE.md`.
   - Write a short plan (files, layers, invariants touched, tests) and wait for approval.
   - Implement in small steps; build and test after each step.
   - Check every item in the task's **Done when** list and the Definition of Done in `CLAUDE.md` §8.
   - Mark the task `[x]` in this file and commit with message `<TASK-ID>: <title>`.
3. Stop at every **🧑 Human checkpoint** and wait for review before continuing.
4. Tasks marked **🧑 Human-owned** are not implemented by Claude Code; only prepare scaffolding or placeholders for them.
5. If a task reveals a missing decision, stop and propose an ADR instead of guessing.

Suggested prompt per task:

```
Implement <TASK-ID> from docs/IMPLEMENTATION_PLAN.md.
Read CLAUDE.md and the referenced sections of docs/ARCHITECTURE.md first.
Start with a plan and wait for my approval before writing code.
```

---

## Phase 0 — Repository bootstrap

Goal: an empty but correctly structured solution that builds, tests and runs in CI.

- [x] **P0-01 Solution skeleton**
  Refs: §4
  - Create `DuetHQ.slnx`, `src/`, `tests/`, `docs/adr/` as in §4.
  - `Directory.Build.props`: .NET 10, nullable, implicit usings, `TreatWarningsAsErrors`, analyzers enabled.
  - `Directory.Packages.props` with central package management.
  - `.editorconfig` with naming and style rules from `CLAUDE.md` §5.
  - Empty projects: `DuetHQ.Web`, `DuetHQ.Web.Client`, three BuildingBlocks, and each module project + `.Contracts` project.
  Done when: `dotnet build -warnaserror` passes; project references follow §5 (modules reference only Contracts).

- [ ] **P0-02 Local environment**
  - `docker-compose.yml` with PostgreSQL 16 and a mail catcher (e.g. Mailpit).
  - `appsettings.Development.json` with placeholders; secrets via user-secrets.
  Done when: `docker compose up` starts dependencies; `dotnet run --project src/DuetHQ.Web` serves a health endpoint.

- [ ] **P0-03 CI pipeline**
  - GitHub Actions: restore, build `-warnaserror`, test (with Testcontainers), fail on warnings.
  Done when: pipeline is green on an empty solution.

- [ ] **P0-04 ADRs 0002–0008**
  Refs: §16
  - Write the remaining seven ADRs listed in §16 (0001 already exists — the codename rename) using a short template (Context, Decision, Consequences, Alternatives).
  Done when: files exist in `docs/adr/` and are linked from §16.

🧑 **Human checkpoint:** review structure, packages and ADRs.

---

## Phase 1 — Guardrails before features

Goal: the rules that protect privacy and structure are executable before any feature exists.

- [ ] **P1-01 Architecture tests**
  Refs: §3, §5.1, CLAUDE.md §3, §9
  - Modules reference other modules only via `.Contracts`.
  - Inside modules: `Domain` has no dependency on `Application`, `Infrastructure`, EF Core, ASP.NET Core.
  - `Application` does not depend on `Infrastructure`.
  - `DuetHQ.Modules.Insights` has no reference to `DuetHQ.Modules.Personal`.
  - No usage of `DateTime.Now` / `DateTime.UtcNow`.
  - Module types are `internal` except Contracts and module registration entry points.
  Done when: tests pass on the skeleton and a deliberately bad reference makes them fail (then remove it).

- [ ] **P1-02 Test infrastructure**
  - Shared Testcontainers PostgreSQL fixture.
  - In-memory Serilog test sink to capture logs.
  - `FakeTimeProvider` helpers.
  Done when: a sample integration test runs against a real container in CI.

---

## Phase 2 — Shared kernel and pure domain core

Goal: all high-risk logic exists as pure, exhaustively tested code before any database or UI.

- [ ] **P2-01 Shared kernel**
  Refs: §4, CLAUDE.md §4
  - `Entity`, `AggregateRoot` (with domain events), `ValueObject` base, `Result`/`Result<T>`/`Error`.
  - Strongly typed IDs: `TenantId`, `MemberId`, `TeamId`, and a generator pattern for the rest.
  - `Locale` value object (`fa`, `en`).
  - `[SensitiveData]` attribute.
  Done when: unit tests cover equality, Result composition, ID parsing.

- [ ] **P2-02 Application abstractions**
  Refs: §3.1, CLAUDE.md §3.3
  - `ICommand`, `ICommand<T>`, `IQuery<T>`, handler interfaces, dispatcher interface.
  - Pipeline behavior abstraction (validation, transaction, tenant context).
  - `ITenantContext`, `ICurrentMember`, `IJobScheduler`.
  Done when: a sample command flows through a validation behavior in a unit test.

- [ ] **P2-03 Tenant calendar**
  Refs: §2, §7.1, §11.2
  - `TenantCalendar` value object, `PeriodKey`, `SlotKey`.
  - `TenantCalendarService`: local date → `PeriodKey` using `WeekStart` and `TimeZoneId`; slot resolution.
  Done when: property tests prove every instant maps to exactly one period; tests cover Saturday-start (Iran), Sunday-start, Monday-start, DST transitions, year boundaries.

- [ ] **P2-04 Emotion reading and scoring**
  Refs: §7.2, §7.3
  - `EmotionStateCode`, `NeedCode`, `EmotionReading` (primary, secondary, intensity, quadrant).
  - `EmotionScoringService` (pure) with weights, secondary threshold, tie-breaking, intensity thresholds.
  Done when: tests cover ties, all-zero weights, single state, secondary threshold boundary, deterministic output for same input.

- [ ] **P2-05 Cohort resolution**
  Refs: §7.3, INV-06
  - `CohortRef` (`Team(id)`, `Leadership`, `Unassigned`).
  - `CohortResolver` (pure).
  Done when: tests cover member, lead of primary team, lead of non-primary team, executive, HR without team, no primary membership.

- [ ] **P2-06 Suppression policy**
  Refs: §9.5, INV-04, INV-09
  - Pure `SuppressionPolicy.Apply(cohorts, minGroupSize)` including complementary suppression and company total.
  - `MinGroupSize` value object rejecting values < 5.
  Done when: property tests show no suppressed cohort count is derivable from visible cohorts + total; unit tests cover all-suppressed, none-suppressed, single hidden cohort of size 1..4.

- [ ] **P2-07 Safety pattern evaluator**
  Refs: §7.4, INV-05
  - Pure evaluator over a member's recent readings.
  Done when: tests cover fewer than K entries, exactly K low entries, interrupted streak.

- [ ] **P2-08 Playbook specifications**
  Refs: §9.6
  - `DominantState`, `NeedFrequency`, `ParticipationDrop`, `IntensityRise` over a window of snapshot values.
  - Suppressed periods break streaks.
  Done when: each specification has positive, negative and suppressed-gap tests.

🧑 **Human checkpoint:** review scoring thresholds, suppression behavior and playbook parameters with real example data.

---

## Phase 3 — Persistence foundation

Goal: schemas, roles, RLS and outbox work and are proven by integration tests.

- [ ] **P3-01 Database roles and schemas script**
  Refs: §10
  - Idempotent SQL script creating schemas and roles with least-privilege grants.
  Done when: integration test verifies each role can only access its allowed schemas (e.g. `duethq_app` cannot select from `pool` or `vault`).

- [ ] **P3-02 Module DbContext base and RLS interceptor**
  Refs: §10, §11
  - Base configuration (snake_case, strongly typed ID converters, value object mapping).
  - Connection interceptor setting `app.tenant_id` per unit of work.
  - Connection string per module role.
  Done when: integration test with two tenants proves rows of tenant A are invisible to tenant B.

- [ ] **P3-03 Transactional outbox**
  - Outbox table per module, background dispatcher, idempotent consumer tracking.
  Done when: tests prove events are published only after commit and redelivery is harmless.

- [ ] **P3-04 Logging redaction**
  Refs: §11.1, INV-10
  - Serilog destructuring policy for `[SensitiveData]`; request logging excludes `/checkin/*` bodies.
  Done when: log capture test shows sensitive values are redacted.

---

## Phase 4 — Organization module

- [ ] **P4-01 Tenant aggregate**
  Refs: §7.1
  - Tenant with calendar, privacy policy, default locale, `Plan = Pilot`.
  - Create/update commands; operator-only endpoint.

- [ ] **P4-02 Teams, members, memberships, roles**
  - Team CRUD, member invite, membership with `TeamRole` and `IsPrimary` invariant, tenant roles.
  - CSV import of members and teams.
  Done when: invariant tests (single primary membership) and import validation tests pass.

- [ ] **P4-03 Privacy notice and acceptance**
  - Versioned notice per locale; acceptance per member; check that blocks check-in without acceptance (exposed via Contracts).
  - 🧑 **Human-owned:** the actual notice text (legal review).

- [ ] **P4-04 Authentication**
  Refs: §11.4
  - Email magic link sign-in (hashed, single-use, short-lived tokens), cookie session.
  - Authorization policies for roles; `CanViewCohort` requirement skeleton.
  Done when: integration tests cover expired, reused and cross-tenant tokens.

- [ ] **P4-05 Admin panel (minimal)**
  - Blazor Server pages: tenant settings, teams, members, roles, CSV import, check-in schedule placeholder.
  - `TenantAdmin` sees no emotional data (INV-08).

🧑 **Human checkpoint:** onboard a fake tenant end to end.

---

## Phase 5 — Content module

- [ ] **P5-01 Content model and seeding**
  Refs: §7.2
  - Entities for model version, states, needs, questions, options with weights, dialog scripts/nodes, translations, strategies, support resources.
  - Seed loader from versioned JSON/YAML in `src/Modules/Content/Seed/`.
  - Published versions immutable (domain rule + test).

- [ ] **P5-02 Initial seed content (draft)**
  - Draft seed for `fa` and `en`: states, needs, 4 questions, options with weights, 2 dialog scripts (Member, Lead), multiple phrasings per node, strategies, global support resources.
  - Clearly marked `DRAFT – requires human review`.
  - 🧑 **Human-owned:** final wording, weights and psychological review.

- [ ] **P5-03 Content query contracts**
  - Queries: get published dialog script by audience and locale; get strategy for reading; get support resources for tenant and locale.

---

## Phase 6 — Check-in and Personal (core vertical slice)

Goal: a member can submit a check-in and get a result, with all privacy invariants proven.

- [ ] **P6-01 Check-in schedule and prompt tokens**
  Refs: §7.3
  - `CheckInSchedule` aggregate, slot generation via `TenantCalendarService`.
  - `PromptToken` create/validate/consume.

- [ ] **P6-02 Personal module: entries and history**
  Refs: §7.4
  - `PersonalEntry` writer contract, history query for the current member only, hard delete on member removal.
  - Safety evaluation exposed only as part of the member's own check-in result.

- [ ] **P6-03 Anonymous buffer and flusher**
  Refs: §9.3, INV-02, INV-03
  - In-memory per-tenant buffer; flusher shuffles and writes batches with `duethq_pool_writer`; flush on shutdown.
  - `pool.anonymous_response` table without timestamps or sequences.

- [ ] **P6-04 Submit check-in command**
  Refs: §9.3 (all steps)
  - Handler: token, consent, window, scoring, cohort, transaction A (participation + outbox), transaction B (personal entry) with compensation, buffer enqueue, result with strategy and optional support resources.
  - Endpoint `/api/v1/checkin/submit` with rate limiting and no body logging.

- [ ] **P6-05 Privacy regression suite**
  Refs: INV-01, INV-02, INV-03, INV-10
  - Integration tests assert:
    - participation and anonymous response are written in different transactions,
    - `pool` rows have no timestamp/sequence columns and no member reference,
    - captured logs contain no answers, readings, needs or safety results,
    - a lead's check-in lands in the Leadership cohort,
    - double submit for the same slot is rejected.

🧑 **Human checkpoint:** review the full check-in path and privacy tests line by line.

---

## Phase 7 — Notifications

- [ ] **P7-01 Channel abstraction and email channel**
  Refs: §7.7
  - `ICheckInChannel`, `EmailChannel` with localized templates and magic link.

- [ ] **P7-02 Prompt scheduler**
  - Background job sends prompts per tenant timezone and slot to members without participation.
  Done when: tests with `FakeTimeProvider` cover timezones and no duplicate prompts.

- [ ] **P7-03 Web push channel**
  - VAPID keys config, subscription storage, send; fallback to email.

---

## Phase 8 — Check-in client and character

- [ ] **P8-01 Dialog engine (client)**
  Refs: §12
  - Blazor WASM client loads the script, runs nodes, picks random phrasing, keeps answers in memory, "Not now" exits without saving.

- [ ] **P8-02 Character component**
  - Placeholder original character (simple shapes/SVG), gentle enter/exit animations, no sound.
  - 🧑 **Human-owned:** final character design.

- [ ] **P8-03 Result, need question and history screens**
  - Result with state label, strategy, optional need, support resources when present; personal history for recent weeks.

- [ ] **P8-04 RTL, accessibility and PWA**
  - `fa`/`en` switching with correct direction; keyboard and screen reader checks; manifest and service worker.
  Done when: manual test checklist in the PR for both locales on mobile and desktop widths.

🧑 **Human checkpoint:** try the check-in as a real user in both languages.

---

## Phase 9 — Insights

- [ ] **P9-01 Participation projection**
  - Consume `ParticipationRecorded`; counts per cohort and period; eligible counts from Organization contracts.

- [ ] **P9-02 Period close and aggregation**
  Refs: §9.4
  - Period close job per tenant timezone + grace; aggregation of anonymous responses; `SuppressionPolicy`; persist `CohortSnapshot`.

- [ ] **P9-03 Playbook engine and signals**
  Refs: §9.6
  - Global versioned rules seed; evaluation over snapshot window; `InsightSignal` lifecycle.

- [ ] **P9-04 Insights queries with authorization and audit**
  Refs: §8, INV-04, INV-12
  - Queries for team snapshots, leadership snapshot, company overview, trends, open signals, signal aging.
  - Policy checks in handlers; every read writes `AccessAuditEntry`; suppressed snapshots return no distributions.
  Done when: tests prove a lead cannot read other teams, suppressed data never leaves the handler, audit rows are written.

---

## Phase 10 — Panels

- [ ] **P10-01 Lead panel**
  - This week, trends, dominant need, participation rate, signals with recommendations, own check-in shortcut.

- [ ] **P10-02 HR and executive panel**
  - Team heatmap by period, participation by team, leadership cohort, signals without action and their age, company overview.

- [ ] **P10-03 Member transparency view**
  - Member sees their primary team's snapshot exactly as the lead sees it, plus access audit summary.

🧑 **Human checkpoint:** review dashboards with seeded demo data for a 30-person fake company.

---

## Phase 11 — Actions

- [ ] **P11-01 Action commitment**
  Refs: §7.6, §9.7
  - Create/update status; link to signal; signal becomes `Addressed`.

- [ ] **P11-02 Team broadcast**
  - `ActionCommitted` → email to team members (localized).

- [ ] **P11-03 Action effect view**
  - In panels, show snapshots before and after a commitment (visible periods only).

---

## Phase 12 — Pilot readiness

- [ ] **P12-01 Pilot feedback**
  - In-app feedback for members, leads and HR; operator view.

- [ ] **P12-02 Deletion jobs**
  Refs: §9.8
  - Member removal and tenant deletion jobs with integration tests.

- [ ] **P12-03 Demo tenant and seed data**
  - Script that creates a fake company with teams, members and generated check-ins across several weeks.

- [ ] **P12-04 Deployment**
  - Dockerfile, production configuration, database migration on deploy, health checks, backups documented.

- [ ] **P12-05 Security and privacy review checklist**
  - Walk through INV-01..INV-12 and produce evidence (test names, code locations) for each in `docs/privacy-evidence.md`.

- [ ] **P12-06 End-to-end smoke test**
  - Invite → accept notice → prompt → check-in → period close → lead sees snapshot → action → team broadcast.

🧑 **Final human checkpoint:** go/no-go for the first pilot tenant.

---

## Human-owned work (parallel to phases)

| Item | Needed before |
|---|---|
| Emotion model wording, weights and strategies review (ideally with an organizational psychologist) | Phase 8 checkpoint |
| Privacy notice and data processing terms | Phase 12 |
| Support resources per pilot country | Phase 12 |
| Character design and name | Phase 8 |
| Pilot company recruitment and interview questions | Phase 12 |
| Legal/payment setup for future international sales | After pilot |

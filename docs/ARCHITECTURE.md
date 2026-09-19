# DuetHQ — Architecture Reference

> Codenamed **Hamdel** (Persian: همدل, "empathetic") until ADR-0001; the codebase now uses the product name **DuetHQ** throughout — solution, namespaces (`DuetHQ.*`), and database roles (`duethq_*`).
> Status: MVP / pilot architecture. This document is the single source of truth for structure and invariants.
> Any change to a section marked **INVARIANT** requires a new ADR in `docs/adr/` and explicit human approval.

---

## 1. Product in one paragraph

Employees answer a short, button-based check-in led by a friendly character. The answers are scored into an emotional state plus an optional "need". The employee immediately gets something back (their state, a small suggestion, their own history). Team leads, HR and executives see **only anonymous, aggregated** weekly snapshots per team, get rule-based signals with suggested actions, and can commit to actions that are announced back to the team. Managers can check in too; their answers are aggregated separately from their team.

**Goals of the MVP:** run free pilots with several companies at once, earn the trust of employees from day one, and collect product feedback — without an architecture that must be rewritten later.

**Non-goals of the MVP:** free-text chat, runtime AI interpretation, Slack/Teams, desktop client, SSO/SCIM, billing, multi-level org trees, PDF reports, anonymous tickets, data residency.

---

## 2. Glossary

| Term | Meaning |
|---|---|
| Tenant | A customer company. Everything is tenant-scoped. |
| Member | A person in a tenant (employee, lead, HR, executive, admin). |
| Team | A flat group of members (MVP: one level, no tree). |
| Membership | Member ↔ Team link with a team role and `IsPrimary`. |
| Check-in slot | One opportunity to answer (e.g. Mon and Thu). Max one answer per member per slot. |
| Period | A calendar week computed from the tenant calendar. Key format `YYYY-Www`. |
| EmotionState | A coded state in the emotion model (e.g. `ANXIETY`). |
| Need | A coded need (e.g. `CLARITY`). Optional per check-in. |
| EmotionReading | Scoring result: primary state, optional secondary, intensity, quadrant. |
| Cohort | The anonymous aggregation bucket a response belongs to: a Team cohort or the tenant Leadership cohort. |
| Snapshot | Aggregated distribution of a cohort for a period, possibly suppressed. |
| Suppression | Hiding aggregated data that could identify individuals. |
| Signal | Result of a playbook rule matching a cohort's snapshots. |
| Action commitment | A lead's public commitment to act on a signal. |

---

## 3. Architectural style

- **Modular monolith**: one deployable ASP.NET Core host, internally split into modules that map to bounded contexts.
- **Clean Architecture inside each module**: `Domain` → `Application` → `Infrastructure`, enforced by architecture tests.
- **DDD tactical patterns**: aggregates, value objects, strongly typed IDs, domain events.
- **CQRS**: commands mutate aggregates; queries read from read models / projections. No shared model for both.
- **Privacy by architecture**: anonymity is guaranteed by data separation, database roles and write timing, not by UI conventions.
- **Deterministic core**: scoring, suppression and playbook rules are pure, versioned and fully unit-tested. No AI at runtime.

### 3.1 Technology

| Concern | Choice |
|---|---|
| Runtime | .NET 10 (LTS), C# latest |
| Host / API | ASP.NET Core, Minimal API endpoint groups per module |
| UI | Blazor Web App. Check-in client = Interactive WebAssembly + PWA manifest/service worker. Panels = Interactive Server. If PWA support in the Web App template is insufficient, the check-in client becomes a standalone Blazor WASM project (same contracts). |
| Database | PostgreSQL 16+, one database, one schema per module, Row-Level Security by tenant |
| ORM | EF Core (one DbContext per module) for writes; EF Core no-tracking or Dapper for read models |
| Messaging | In-process mediator behind our own abstractions (`ICommandHandler<>`, `IQueryHandler<>`). MediatR allowed only behind these abstractions and only if its license terms are acceptable. |
| Integration events | Transactional outbox per module, in-process dispatcher |
| Background jobs | `BackgroundService` + `PeriodicTimer` (MVP). Swap to Quartz/Hangfire later behind `IJobScheduler`. |
| Logging | Serilog with redaction policy (see §11) |
| Email | SMTP / provider behind `IEmailSender` |
| Web push | VAPID Web Push behind `IWebPushSender` |
| Tests | xUnit, Shouldly, Testcontainers (PostgreSQL), NetArchTest, FsCheck (property tests) |

---

## 4. Solution layout

```
DuetHQ.slnx
CLAUDE.md
docs/
  ARCHITECTURE.md
  adr/
    0001-codename-rename-hamdel-to-duethq.md
    0002-modular-monolith.md
    0003-three-way-split-write.md
    ...
src/
  DuetHQ.Web/                         Composition root, endpoints mapping, auth, panels (Blazor Server)
  DuetHQ.Web.Client/                  Check-in client (Blazor WASM), character UI, PWA
  BuildingBlocks/
    DuetHQ.SharedKernel/              Entity, AggregateRoot, ValueObject, Result/Error, StronglyTypedId,
                                      TenantId, MemberId, PeriodKey, SlotKey, Locale
    DuetHQ.Application.Abstractions/  ICommand, IQuery, handlers, IUnitOfWork, ITenantContext,
                                      ICurrentMember, IJobScheduler, validation pipeline
    DuetHQ.Infrastructure.Common/     EF base config, RLS connection interceptor, outbox,
                                      logging redaction, TimeProvider registration
  Modules/
    Organization/
      DuetHQ.Modules.Organization/            (Domain/, Application/, Infrastructure/, Endpoints/)
      DuetHQ.Modules.Organization.Contracts/  (public queries, DTOs, integration events)
    Content/        (+ .Contracts)
    CheckIn/        (+ .Contracts)
    Personal/       (+ .Contracts)
    Insights/       (+ .Contracts)
    Actions/        (+ .Contracts)
    Notifications/  (+ .Contracts)
    Pilot/          (+ .Contracts)
tests/
  DuetHQ.ArchitectureTests/
  DuetHQ.IntegrationTests/
  Modules/DuetHQ.Modules.<Name>.Tests/
```

Each module project contains folders `Domain`, `Application`, `Infrastructure`, `Endpoints`. Types are `internal` by default. Other modules may reference **only** `<Module>.Contracts`.

---

## 5. Modules (bounded contexts)

| Module | Schema | Owns | Depends on (Contracts) |
|---|---|---|---|
| Organization | `org` | Tenant, TenantCalendar, PrivacyPolicy, Team, Member, Membership, TenantRoleAssignment, PrivacyNotice, PrivacyNoticeAcceptance | — |
| Content | `content` | ModelVersion, EmotionState, Need, Question, AnswerOption, DialogScript, DialogNode, Translation, PersonalStrategy, SupportResource | — |
| CheckIn | `checkin` | CheckInSchedule, PromptToken, Participation, scoring, the split-write orchestration, anonymous buffer | Organization, Content, Personal, Insights |
| Personal | `vault` | PersonalEntry, personal history read model, safety pattern evaluation | Content |
| Insights | `pool`, `insights` | AnonymousResponse, CohortSnapshot, PlaybookRule, InsightSignal, AccessAuditEntry, participation projection | Organization, Content, CheckIn (participation counts only) |
| Actions | `actions` | ActionCommitment | Organization, Insights |
| Notifications | `notify` | Delivery of prompts (email, web push), team broadcasts, WebPushSubscription | Organization |
| Pilot | `pilot` | PilotFeedback | Organization |

### 5.1 Module rules

- No module reads another module's schema. Cross-module data flows only through Contracts (sync queries) or integration events (async).
- No foreign keys across schemas. Cross-module references are IDs (strongly typed) only.
- `Insights` never receives `MemberId` together with emotional content — ever (see INV-01).
- `Personal` never publishes events that leave the module containing emotional content or safety state (see INV-05).

---

## 6. Privacy invariants — **INVARIANT**

| ID | Invariant |
|---|---|
| INV-01 | No data path links an emotional answer to a person, except the person's own `PersonalEntry` in `vault`. |
| INV-02 | Every check-in is persisted as three unlinkable records: `PersonalEntry` (vault), `Participation` (checkin), `AnonymousResponse` (pool). They share no key, no timestamp, and are **not written in the same database transaction**. |
| INV-03 | `AnonymousResponse` has a random UUIDv4 id and **no timestamp columns** (no `created_at`, no `updated_at`, no identity/sequence). Time granularity is `PeriodKey` only. |
| INV-04 | Aggregated data is shown only through `CohortSnapshot` after suppression. No endpoint, export or UI reads `pool` directly except the Insights aggregator. |
| INV-05 | Safety detection happens only inside `Personal`, is shown only to the member, and is never exposed to leads, HR, executives, admins or logs. |
| INV-06 | A lead's or executive's own check-in goes to the tenant Leadership cohort, never to a team cohort they can view. |
| INV-07 | Participation is voluntary. Viewers see participation **rates**, never who did or did not answer. |
| INV-08 | `TenantAdmin` sees configuration only, no emotional data. No support/impersonation access to `vault` or `pool`. |
| INV-09 | Minimum group size is ≥ 5 and cannot be configured lower. |
| INV-10 | The check-in submission request body and emotional payloads are never logged, traced or included in exceptions. |
| INV-11 | No free text is collected in check-ins (MVP). No runtime AI interprets emotions. |
| INV-12 | Every read of aggregated insights by a viewer writes an `AccessAuditEntry`. |

---

## 7. Domain model

Notation: **Aggregate** (bold), value objects in `code`. All entities carry `TenantId` except global reference data.

### 7.1 Organization (`org`)

- **Tenant**: `TenantId`, Name, `DefaultLocale`, `TenantCalendar` { `WeekStart` (DayOfWeek), `TimeZoneId`, WorkingDays }, `PrivacyPolicy` { `MinGroupSize` (≥5) }, `Plan` (`Pilot` | future values), Status.
- **Team**: `TeamId`, Name, IsActive.
- **Member**: `MemberId`, Email, DisplayName, `Locale`, `TimeZoneId?`, Status (Invited/Active/Removed), `TenantRoles` set of { `HR`, `Executive`, `TenantAdmin` }.
- **Membership** (owned by Member): `TeamId`, `TeamRole` (`Member` | `Lead`), `IsPrimary`, From, To?.
  - Invariant: an active member has at most one primary membership.
- **PrivacyNotice**: Version, Locale, Text, PublishedAt.
- **PrivacyNoticeAcceptance**: `MemberId`, NoticeVersion, AcceptedAt. A member cannot check in before accepting the current version.

### 7.2 Content (`content`) — global reference data, versioned, immutable after publish

- **ModelVersion**: `ModelVersionId`, Label, Status (Draft/Published/Retired). Published versions are immutable.
- **EmotionState**: Code (e.g. `ANXIETY`), `Energy` (-2..2), `Pleasantness` (-2..2), SortOrder.
- **Need**: Code (`CLARITY`, `RECOGNITION`, `AUTONOMY`, `CONNECTION`, `FAIRNESS`, `REST`).
- **Question**: Code, `Dimension` (`MindTime` | `Body` | `ActionTendency` | `InnerVoice`), Order.
- **AnswerOption**: Code, QuestionCode, `Weights` map EmotionStateCode → decimal.
- **DialogScript**: per ModelVersion and `Audience` (`Member` | `Lead`); ordered **DialogNode**s.
- **DialogNode**: NodeCode, Kind (`Greeting` | `Question` | `NeedQuestion` | `Result` | `Closing`), QuestionCode?, PromptVariantKeys[], NextNodeRule.
- **Translation**: (Key, Locale) → Text. All user-visible content is a translation key. Codes are language-neutral.
- **PersonalStrategy**: StateCode, NeedCode?, TextKey.
- **SupportResource**: TenantId? (null = global default), Locale, TitleKey, Contact, Kind.

Content is seeded from versioned files in `src/Modules/Content/Seed/` and reviewed by a human (ideally an organizational psychologist) before publishing.

### 7.3 CheckIn (`checkin`)

- **CheckInSchedule** (per tenant): Slots per week (e.g. Mon 10:00, Thu 15:00 in tenant time), response window hours.
- **PromptToken**: TokenHash, `MemberId`, `SlotKey`, ExpiresAt, UsedAt?. Single use. Used for magic-link entry.
- **Participation**: `MemberId`, `SlotKey`, `PeriodKey`, `CohortRef`. Unique (MemberId, SlotKey). No emotional content. No timestamp beyond what `SlotKey` implies.
- `SlotKey` = `PeriodKey` + slot index (e.g. `2026-W38-1`).
- Domain service **EmotionScoringService** (pure): answers + ModelVersion → `EmotionReading`.
  - Sum weights per state; primary = max; secondary = second max if ≥ 60% of primary; ties broken by SortOrder.
  - Intensity 1–3 from normalized primary score thresholds defined in the ModelVersion.
  - Quadrant from the primary state's Energy/Pleasantness.
- Domain service **CohortResolver** (pure): member roles + primary membership → `CohortRef`.
  - Lead of primary team, or has `Executive` → `Leadership` cohort of tenant.
  - Otherwise primary team → `Team(teamId)` cohort.
  - No primary team → `Unassigned` cohort (company total only).

### 7.4 Personal (`vault`)

- **PersonalEntry**: `PersonalEntryId`, `MemberId`, LocalDate, ModelVersionId, `EmotionReading`, NeedCode?, AnswerCodes[].
- Read model **PersonalHistory**: last N weeks of readings and needs for the member.
- Domain service **SafetyPatternEvaluator** (pure): if the last K entries (default K=4) all have Pleasantness ≤ -2 (or configured rule) → return `ShowSupport`. Result is returned in the check-in response to the member only.
- Member removal hard-deletes all their `PersonalEntry` rows.

### 7.5 Insights (`pool`, `insights`)

- **AnonymousResponse** (`pool`): Id (UUIDv4), TenantId, `CohortRef`, `PeriodKey`, ModelVersionId, PrimaryStateCode, SecondaryStateCode?, Intensity, NeedCode?. **No timestamps. No member reference.**
- **CohortSnapshot** (`insights`): TenantId, `CohortRef`, `PeriodKey`, ModelVersionId, RespondentCount, EligibleCount, IsSuppressed, `SuppressionReason?` (`BelowThreshold` | `Complementary`), StateDistribution, NeedDistribution, ComputedAt.
- **PlaybookRule** (global, versioned): Code, `ConditionType`, Parameters (JSON), RecommendationKey, Severity.
- **InsightSignal**: TenantId, `CohortRef`, PeriodKey, RuleCode, Evidence (JSON of snapshot values), Status (`Open` | `Addressed` | `Expired`).
- **AccessAuditEntry**: TenantId, ViewerMemberId, ViewerRole, `CohortRef`, PeriodKey, ViewedAt.
- Participation projection: per (CohortRef, PeriodKey) respondent count derived from `ParticipationRecorded` events (counts only).

### 7.6 Actions (`actions`)

- **ActionCommitment**: TenantId, `TeamId`, SignalId?, Title, Description?, Status (`Planned` | `InProgress` | `Done` | `Declined`), DeclineReason?, CreatedByMemberId, CreatedAt, UpdatedAt. Leads are accountable, so this aggregate is **not** anonymous.

### 7.7 Notifications (`notify`)

- **WebPushSubscription**: MemberId, Endpoint, Keys.
- Port **ICheckInChannel** (Strategy): `EmailChannel`, `WebPushChannel`. Future: `TeamsChannel`, `SlackChannel`, `DesktopCompanionChannel`.
- Team broadcasts for action commitments (email in MVP).

### 7.8 Pilot (`pilot`)

- **PilotFeedback**: TenantId, MemberId?, Role, Rating (1–5), Category, Text, CreatedAt. Contains no emotional check-in data. Free text allowed here only.

---

## 8. Roles and visibility

| Role | Own vault | Team cohorts | All tenant cohorts | Leadership cohort | Company total | Config |
|---|---|---|---|---|---|---|
| Member | ✓ | transparency view of own primary team | — | — | — | — |
| Lead (TeamRole) | ✓ | teams they lead | — | — | — | — |
| Executive | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| HR | ✓ | ✓ | ✓ | ✓ | ✓ | members, teams |
| TenantAdmin | ✓ | — | — | — | — | ✓ |

- Authorization uses ASP.NET Core policies with requirements, e.g. `CanViewCohortRequirement(CohortRef)`.
- The "transparency view" shows a member exactly the snapshot their lead sees, plus the access audit of who viewed it.

---

## 9. Key workflows

### 9.1 Onboarding a pilot tenant

```
Operator creates Tenant (Plan=Pilot, calendar, locale)
→ HR/Admin creates Teams, invites Members (CSV or form), assigns roles
→ Members receive invite email → accept PrivacyNotice
→ HR configures CheckInSchedule
```

### 9.2 Prompt

```
PromptScheduler (per tenant timezone, per slot)
→ for each active member without Participation for SlotKey:
     create PromptToken
     ICheckInChannel.SendPromptAsync (email magic link / web push)
```

### 9.3 Check-in and three-way split — **INVARIANT (INV-01..03, INV-10)**

```
Member opens magic link → token validated (not yet consumed)
→ Check-in client loads DialogScript for audience + locale
→ Dialog runs fully client-side (answers held in memory)
→ POST /checkin/submit { token, answerCodes[], needCode? }        ← body never logged

SubmitCheckInCommandHandler:
  1. Validate token, consent, slot window; consume token
  2. EmotionScoringService → EmotionReading
  3. CohortResolver → CohortRef
  4. Transaction A (checkin schema):
       consume PromptToken
       insert Participation(MemberId, SlotKey, PeriodKey, CohortRef)   ← unique key guards double submit
       outbox: ParticipationRecorded(TenantId, CohortRef, PeriodKey)   ← counts only, no MemberId
  5. Transaction B (vault schema, via Personal.Contracts IPersonalEntryWriter):
       insert PersonalEntry(MemberId, LocalDate, Reading, Need, Answers)
       on failure → compensating command removes Participation and re-opens the token; return error
  6. AnonymousBuffer.Enqueue(AnonymousResponseDraft)                 ← in memory, no identity, only after 4 and 5 succeed
  7. SafetyPatternEvaluator on member history (inside Personal)
  8. Return CheckInResult { Reading, Strategy, SupportResources? }

AnonymousBufferFlusher (background):
  - flush per tenant when buffer ≥ MinGroupSize items OR every 15 minutes
  - shuffle items, insert in a single transaction using the pool writer role
  - flush on graceful shutdown
```

**Accepted trade-off (ADR-0003):** a hard crash may lose buffered anonymous responses. Participation may then slightly exceed respondent count. This is preferable to any durable store that correlates identity and content (same transaction id, insertion order, timestamps).

### 9.4 Period close and aggregation

```
PeriodCloseJob: when a period ends in tenant timezone + grace (24h)
→ Insights.AggregatePeriodCommand(TenantId, PeriodKey)
   → load AnonymousResponses per CohortRef
   → SuppressionPolicy.Apply(cohorts, eligibleCounts, MinGroupSize)
   → persist CohortSnapshots
   → PlaybookEngine.Evaluate(recent snapshots window) → InsightSignals
   → publish SnapshotsComputed(TenantId, PeriodKey)
```

### 9.5 Suppression policy (pure function) — **INVARIANT (INV-04, INV-09)**

```
Input: team/leadership/unassigned cohorts with RespondentCount for one tenant+period, MinGroupSize
1. Primary: cohort with RespondentCount < MinGroupSize → suppressed (BelowThreshold)
2. Company total (sum of all cohorts):
     hiddenSum = sum(RespondentCount of suppressed cohorts)
     if hiddenSum > 0 and hiddenSum < MinGroupSize:
         suppress the smallest visible cohort (Complementary) and recompute
         repeat until hiddenSum == 0 or hiddenSum ≥ MinGroupSize or no visible cohorts remain
3. Company total itself is suppressed if total < MinGroupSize
Output: snapshots with IsSuppressed and reason; suppressed snapshots expose no distributions
```

Property test: for any generated tenant, no suppressed cohort's distribution can be derived from visible snapshots and the company total.

### 9.6 Playbook evaluation

- `ConditionType` is a closed set implemented as specifications in code, parameterized from data:
  - `DominantState` — state share ≥ X% for N consecutive visible periods
  - `NeedFrequency` — need share ≥ X% for N consecutive visible periods
  - `ParticipationDrop` — participation rate dropped by ≥ X points vs trailing average
  - `IntensityRise` — average intensity rising for N periods
- Suppressed periods break a streak (never inferred through).
- Recommendation text is a fixed, reviewed translation key with probabilistic wording.

### 9.7 Viewing insights and committing to action

```
Lead/HR opens panel → policy check → query snapshots (read model)
→ write AccessAuditEntry per cohort+period viewed
→ Lead creates ActionCommitment (optionally linked to signal)
→ ActionCommitted event → Notifications broadcasts to team members
→ Signal status → Addressed
```

### 9.8 Leaving and deletion

- Member removed → hard delete `PersonalEntry`, `PromptToken`, `WebPushSubscription`; `Participation` anonymized to counts or deleted; `AnonymousResponse` untouched (unlinkable by design).
- Tenant deleted → drop all tenant rows in every schema via a documented job.

---

## 10. Persistence

- One PostgreSQL database; schemas per §5; `snake_case` naming.
- Every tenant-scoped table has `tenant_id` and an RLS policy `tenant_id = current_setting('app.tenant_id')::uuid`. A DbConnection interceptor sets `app.tenant_id` per unit of work.
- Database roles (least privilege):
  - `duethq_checkin` → `checkin` RW, `org` R, `content` R
  - `duethq_vault` → `vault` RW
  - `duethq_pool_writer` → `pool` INSERT only
  - `duethq_insights` → `pool` SELECT, `insights` RW, `org` R, `content` R
  - `duethq_app` → `org`, `actions`, `notify`, `pilot`, `content` as needed; **no** access to `pool` or `vault`
- Each module has its own EF Core migrations assembly/folder and migration history table in its schema.
- `pool.anonymous_response`: primary key UUID generated in application (v4), no default timestamp columns, no serial columns.
- Read models may be materialized tables or views owned by the module that serves them.

---

## 11. Cross-cutting concerns

### 11.1 Logging, tracing, errors
- Serilog destructuring policy redacts types marked `[SensitiveData]` (answers, readings, needs, tokens).
- Request logging and tracing exclude bodies for `/checkin/*`.
- From step 2 of §9.3 onward, logs in the check-in pipeline must not include `MemberId` alongside any reading, answer or need.
- Exceptions never include payloads. Problem details are generic.

### 11.2 Time
- Inject `TimeProvider`. Never `DateTime.Now`/`UtcNow` directly.
- `PeriodKey` and `SlotKey` computed only by `TenantCalendarService` using tenant `WeekStart` and `TimeZoneId`.

### 11.3 Localization
- Locales in MVP: `fa`, `en`. UI strings via resource files; domain content via `content.translation`.
- `dir="rtl"` for `fa`. Layout must be verified in both directions.

### 11.4 Security
- Authentication: email magic link for check-in entry and panel sign-in (cookie session). Tokens hashed at rest, single use, short-lived.
- Authorization: policy-based, evaluated in application layer queries (not only in UI).
- CSRF protection for Blazor Server forms; rate limiting on submit and sign-in endpoints.

### 11.5 Validation and errors
- `Result<T>`/`Error` for expected failures. Exceptions only for programmer errors and infrastructure faults.
- FluentValidation-style validators (or hand-written) in the command pipeline.

---

## 12. Character and conversation (UI behavior)

- The character is original to this product. It asks, it never infers ("How are you right now?", never "You seem tired").
- Level 1 conversation only: scripted nodes, button answers, varied pre-written phrasings chosen at random per node.
- "Not now" always available; no answer = not now. The character never repeats a prompt within the same slot.
- Result screen: state label, one strategy, optional need question, link to personal history, and support resources only when the safety evaluator says so.
- Tone: calm, peer-level, brief. No forced cheerfulness; no jokes after a low-pleasantness result.

---

## 13. Testing strategy

| Layer | What | Tools |
|---|---|---|
| Architecture | Module isolation, layering, invariant guards (e.g. `Insights` has no reference to `Personal`; no `DateTime.Now`) | NetArchTest |
| Domain | Scoring, cohort resolution, suppression, playbook specs, safety evaluator — exhaustive unit + property tests | xUnit, FsCheck, Shouldly |
| Application | Command/query handlers with in-memory fakes | xUnit |
| Integration | Real PostgreSQL: RLS isolation between tenants, DB role permissions, split write, no timestamps in pool | Testcontainers |
| Privacy regression | Log sink assertion: submitting a check-in writes no reading/answers to logs | xUnit + test sink |

---

## 14. Deferred (designed-for, not built)

SSO/SCIM (`Member.ExternalIdentity` later), Slack/Teams/desktop channels (`ICheckInChannel`), multi-level org tree (`Team.ParentTeamId` later; suppression already works on cohort sets), tenant data residency (`Tenant.Region` later + connection resolver), crypto-shredding of vault, PDF reports, anonymous feedback tickets (separate module, never in `pool`), billing module (`Plan` already exists), model migration maps between versions, context tags, differential privacy noise in aggregation.

## 15. Known limitations (MVP)

- Buffered anonymous responses can be lost on crash (§9.3).
- Week-over-week comparison of small cohorts can hint at changes when membership changes; mitigated by threshold, not eliminated.
- Database superusers and WAL-level access could theoretically correlate timing; operational access is restricted and audited.

## 16. ADR index (initial)

| ADR | Decision |
|---|---|
| [0001](adr/0001-codename-rename-hamdel-to-duethq.md) | Codename rename: `Hamdel` → `DuetHQ` across solution, namespaces and database roles |
| [0002](adr/0002-modular-monolith.md) | Modular monolith with Clean Architecture per module |
| [0003](adr/0003-three-way-split-write.md) | Three-way split write with in-memory shuffled buffer for anonymous responses |
| [0004](adr/0004-no-runtime-ai-scripted-conversation.md) | No runtime AI; scripted level-1 conversation |
| [0005](adr/0005-cohorts-and-leadership-cohort.md) | Cohorts and separate Leadership cohort |
| [0006](adr/0006-suppression-min-group-size.md) | Suppression with complementary suppression, min group size ≥ 5 |
| [0007](adr/0007-notification-channels-as-adapters.md) | Web + email + web push first; channels as adapters |
| [0008](adr/0008-own-mediator-abstractions.md) | Own mediator abstractions; third-party mediator optional |

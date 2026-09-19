# 0003. Three-way split write with in-memory shuffled buffer for anonymous responses

## Status

Accepted

## Context

A check-in produces three kinds of facts: the member's own reading (needed for personal history and safety support), proof that the member participated (needed for participation rates and the "one answer per slot" rule), and the anonymous response that feeds team aggregates. If any store can link a person to their emotional answer other than the member's own vault entry, the product's central promise (INV-01) is broken.

Simply writing three tables in one transaction would leave correlatable traces: a shared transaction id, insertion order, sequence values and timestamps could all re-identify an "anonymous" row by joining it to the participation row written at the same moment.

## Decision

Every check-in is persisted as **three unlinkable records** (INV-02, `docs/ARCHITECTURE.md` §9.3):

| Record | Schema | Contains | Contains no |
|---|---|---|---|
| `PersonalEntry` | `vault` | member, reading, need, answers | anything shared with the other two |
| `Participation` | `checkin` | member, slot, period, cohort | emotional content |
| `AnonymousResponse` | `pool` | tenant, cohort, period, state, intensity, need | member reference, timestamp, sequence/identity column (INV-03) |

Rules that make the separation real:

- `Participation` and `PersonalEntry` are written in **different transactions** (A in `checkin`, B in `vault` via `Personal.Contracts`). If B fails, a compensating command removes the `Participation` and re-opens the token.
- `AnonymousResponse` is **never written in the same transaction** as either. It is enqueued in an **in-memory `AnonymousBuffer`** only after A and B succeed.
- A background **flusher** shuffles buffered items and writes them in one batch with the `duethq_pool_writer` role (INSERT-only). It flushes per tenant when the buffer reaches `MinGroupSize` items or every 15 minutes, and on graceful shutdown.
- `AnonymousResponse.Id` is an application-generated UUIDv4; the table has no `DEFAULT now()`, no serial column, no timestamps. Time granularity is `PeriodKey` only.
- The integration event `ParticipationRecorded` carries counts only, never `MemberId` together with content.

## Consequences

- **Accepted trade-off:** a hard crash can lose buffered anonymous responses, so a period's participation count may slightly exceed its respondent count (`docs/ARCHITECTURE.md` §15). This is preferred over any durable store that correlates identity and content.
- Write ordering and shuffling remove insertion-order and timing as re-identification channels at the application level. Database superusers and WAL-level access could theoretically still correlate timing; operational access is restricted and audited (§15).
- The check-in handler is a small orchestration with explicit compensation instead of a single atomic transaction, and needs thorough tests (P6-04, P6-05): different transactions, no timestamp/sequence columns in `pool`, no reading/answer/need in captured logs, double submit rejected.
- Flush cadence trades data freshness for anonymity: small tenants may see responses appear only at the 15-minute flush or at shutdown.

## Alternatives considered

- **One transaction for all three writes.** Rejected: shared transaction id, ordering and timestamps link the rows; it directly violates INV-02.
- **Durable queue or outbox for anonymous responses.** Rejected: a durable queue stores content in arrival order with timestamps, which re-creates the correlation this design removes. The lost-on-crash risk is accepted instead.
- **Write `AnonymousResponse` immediately, without buffering.** Rejected: the row would appear next to the participation row in time and order, even without a timestamp column.
- **Cryptographic unlinking or differential-privacy noise.** Deferred (`docs/ARCHITECTURE.md` §14); not needed for the pilot and adds complexity to the core path.

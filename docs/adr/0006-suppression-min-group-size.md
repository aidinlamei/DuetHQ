# 0006. Suppression with complementary suppression, minimum group size ≥ 5

## Status

Accepted

## Context

Showing aggregates of very small groups can identify individuals. Hiding only the small groups is not enough: if a company total and the other cohorts are visible, a hidden cohort's numbers can be recovered by subtraction. Anonymity must hold for the combination of everything a viewer can see, not just for each number alone (INV-04, INV-09; `docs/ARCHITECTURE.md` §9.5).

## Decision

Suppression is a **pure function**, `SuppressionPolicy.Apply(cohorts, minGroupSize)`, and is the only path by which aggregates reach a viewer:

1. **Primary suppression:** a cohort with `RespondentCount < MinGroupSize` is suppressed (`BelowThreshold`).
2. **Complementary suppression:** let `hiddenSum` be the total respondents of suppressed cohorts. If `0 < hiddenSum < MinGroupSize`, the hidden sizes could be inferred from the company total, so the smallest visible cohort is also suppressed (`Complementary`) and the check repeats until `hiddenSum == 0`, `hiddenSum ≥ MinGroupSize`, or no visible cohorts remain.
3. **Company total** is itself suppressed if the total is below `MinGroupSize`.
4. Suppressed snapshots **expose no distributions**.

Guardrails:

- `MinGroupSize` is a value object that **rejects values below 5** and cannot be configured lower (INV-09).
- Only `CohortSnapshot` is exposed to viewers, and only after suppression; nothing reads `pool` outside the Insights aggregator (INV-04). No ad-hoc aggregate may bypass `SuppressionPolicy`.
- Playbook rules treat a suppressed period as a **break in a streak**; values are never inferred across it (`docs/ARCHITECTURE.md` §9.6).
- Every read of aggregated insights writes an `AccessAuditEntry` (INV-12).

## Consequences

- The strongest properties are testable: a property test must show that no suppressed cohort's distribution can be derived from visible snapshots plus the company total (P2-06).
- Some visible cohorts are hidden purely to protect others, which can surprise users; the UI should present suppression neutrally, and the reason is recorded on the snapshot.
- Small tenants or teams will often see nothing. That is the intended trade for trust, and it makes participation rate and team size a practical pilot concern.
- Week-over-week comparison of small cohorts can still hint at changes when membership changes; the threshold mitigates this but does not eliminate it (`docs/ARCHITECTURE.md` §15).
- Raising the threshold later is possible; lowering it below 5 is not without a new ADR and explicit human approval.

## Alternatives considered

- **Primary suppression only.** Rejected: a hidden cohort can be computed as total minus visible cohorts.
- **Differential-privacy noise in aggregation.** Deferred (`docs/ARCHITECTURE.md` §14); adds complexity and interpretability cost that the pilot does not need.
- **Configurable per-tenant threshold, including values under 5.** Rejected: a tenant could weaken anonymity for its own employees, which is exactly the risk INV-09 forbids.
- **Rounding or bucketing values instead of suppressing.** Rejected: with small counts it still leaks information and complicates the pure-function guarantees.

# 0005. Cohorts and separate Leadership cohort

## Status

Accepted

## Context

Aggregated insights need a bucket to aggregate into. A team is the natural bucket, but leads and executives also check in. If a lead's answers were aggregated into the team they can view, they could recognize their own contribution and, in small teams, infer the answers of others by elimination. Executives and HR see many teams, so the same risk applies at tenant level. Members without a primary team must also be handled without breaking anonymity.

## Decision

Every response and every participation belongs to exactly one **cohort** (`CohortRef`), resolved by the pure domain service `CohortResolver` (`docs/ARCHITECTURE.md` §7.3):

- **`Team(teamId)`**: a member whose primary team is that team and who is neither a lead of it nor an executive.
- **`Leadership`**: a lead of their primary team, or anyone with the `Executive` tenant role. There is one Leadership cohort per tenant (INV-06).
- **`Unassigned`**: a member with no primary team. Contributes to the company total only.

Consequences of the rule for the code:

- A lead's or executive's own check-in **never** lands in a team cohort they can view. `CohortResolver` is the single place that decides this; it must not be special-cased elsewhere (`CLAUDE.md` §2).
- Drill-down follows cohorts only. There are **no filters that combine dimensions** (team × role × tenure and similar).
- Participation is shown as rates per cohort, never as a list of who did or did not answer (INV-07).
- The Leadership cohort is subject to the same suppression rules as any other cohort (ADR-0006).
- An active member has at most one primary membership, which keeps the cohort assignment unambiguous.

## Consequences

- Leads and executives can use the product without compromising the anonymity of their teams.
- Leadership insights only become visible when enough leaders participate (`MinGroupSize`), so small companies may see no Leadership snapshot.
- Insights are coarser than a fully flexible slicing model: no cross-dimension analytics by design.
- The structure supports a future multi-level org tree: suppression already operates on a set of cohorts (`docs/ARCHITECTURE.md` §14).
- `CohortResolver` needs full unit coverage: member, lead of primary team, lead of a non-primary team, executive, HR without a team, and no primary membership (P2-05).

## Alternatives considered

- **Leads counted in their team's cohort.** Rejected: leads see the team snapshot, so they could identify their own contribution and infer others' in small teams.
- **Leads and executives do not check in.** Rejected: managers' wellbeing matters to the product, and excluding them would make the tool feel like surveillance of others only.
- **Free filtering across team, role and tenure.** Rejected: combining dimensions produces small groups that can identify individuals and defeats suppression.
- **One cohort per role type.** Rejected: it adds more small cohorts and more complementary suppression without improving usefulness for the pilot.

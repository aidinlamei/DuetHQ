# 0001. Codename rename: `Hamdel` → `DuetHQ`

## Status

Accepted

## Context

The project was scaffolded (task P0-01) under the internal codename `Hamdel`, per the original split documented in `docs/NAMING.md`: code, namespaces and database roles would keep the codename `Hamdel` while customer-facing text would show the commercial brand `Duet HQ` / `Duet`.

Immediately after P0-01 was committed, with no domain/application code written yet, the product owner asked to drop that split and use the product name directly in the codebase instead of carrying two names through the rest of development.

## Decision

Rename every identifier derived from `Hamdel` to `DuetHQ` (PascalCase, no space — the identifier form of the brand):

- Solution file: `Hamdel.slnx` → `DuetHQ.slnx`.
- Every project and its containing folder: `Hamdel.*` → `DuetHQ.*` (e.g. `Hamdel.Web` → `DuetHQ.Web`, `Hamdel.Modules.CheckIn` → `DuetHQ.Modules.CheckIn`, `Hamdel.Modules.CheckIn.Contracts` → `DuetHQ.Modules.CheckIn.Contracts`, and all `Hamdel.*Tests` projects).
- All C#/Razor namespaces and `using`/`typeof` references matching the above.
- Documented database roles in `docs/ARCHITECTURE.md` §10: `hamdel_*` → `duethq_*` (`duethq_checkin`, `duethq_vault`, `duethq_pool_writer`, `duethq_insights`, `duethq_app`). No database roles exist yet (Phase 3), so this is a documentation-only change at this point.
- `docs/NAMING.md` rewritten: the codename/brand split becomes an identifier-form/display-form split — `DuetHQ` (no space) in code, `Duet HQ` / `Duet` (with space) in anything a person reads. See `docs/NAMING.md` for the current rules.
- `docs/ARCHITECTURE.md` §16 ADR index renumbered: this ADR is 0001; the seven ADRs originally planned as 0001–0007 shift to 0002–0008 (none were written yet, so no renumbering of existing files was needed).

## Consequences

- Every path, namespace and doc reference written during P0-01 needed updating in the same change; done via `git mv` for files/folders (preserves history) plus a scripted content replace, verified by a full rebuild (`dotnet build -warnaserror`) and test run (`dotnet test`).
- `docs/NAMING.md`'s rule 6 (a test forbidding the bare string in user-facing text) now targets the identifier form `DuetHQ` instead of `Hamdel` — still relevant once Phase 8+ builds user-facing UI, since `DuetHQ` (no space) must never leak into text a person reads; only `Duet HQ` / `Duet` may.
- Future ADRs (originally 0001–0007) are now 0002–0008; anyone referencing the old numbers from before this change should re-check `docs/ARCHITECTURE.md` §16.
- No effect on any privacy invariant (INV-01…INV-12) — this is a naming-only change with no data-flow or schema-boundary implications.

## Alternatives considered

- **Keep `Hamdel` as the codename, rename only later per `docs/NAMING.md`'s original plan.** Rejected: the product owner asked for the rename now, and doing it before any domain code exists is the cheapest point in the project to do it.
- **Use `Duet` (short form) instead of `DuetHQ` as the identifier prefix.** Rejected: `Duet` is generic enough to risk collisions with unrelated packages/namespaces; `DuetHQ` matches the full commercial name and was the form requested.

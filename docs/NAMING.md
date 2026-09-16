# Product Naming

## Decision

| Item | Value |
|---|---|
| Commercial product name | **Duet HQ** |
| Short name inside the product UI | **Duet** |
| Solution / namespace / identifier prefix | **`DuetHQ`** |

The product is marketed and presented to customers as **Duet HQ**.

As of ADR-0001 (`docs/adr/0001-codename-rename-hamdel-to-duethq.md`), the codebase no longer uses a separate internal codename (`Hamdel`). Solution, projects, namespaces (`DuetHQ.*`), and database roles (`duethq_*`) now use the identifier form of the product name directly. The only remaining split is **identifier form vs. display form**, not two different words:

- Identifier form (code, schemas, config keys): `DuetHQ` — no space, PascalCase, e.g. `DuetHQ.Modules.CheckIn`, `duethq_checkin`.
- Display form (anything a person reads): `Duet HQ` (first mention) / `Duet` (later mentions, in-app labels) — always with the space, never translated.

## Rules for Claude Code

1. **Code and internal names use `DuetHQ`.** Solution, projects, namespaces, database roles (`duethq_*`), schemas, configuration sections and internal docs use this identifier form. Do not introduce a different codename.
2. **Everything a customer or employee sees uses the display form.** Page titles, emails, notifications, PWA manifest, privacy notice, dashboards, error pages and the check-in client show **Duet HQ** or **Duet**, never the bare identifier `DuetHQ`.
3. **Never hardcode the brand name in UI or templates.** Read it from a single source:
   - configuration: `Branding:ProductName = "Duet HQ"`, `Branding:ShortName = "Duet"`
   - exposed through an `IBrandingOptions` (Options pattern) and localization placeholders such as `{ProductName}`.
4. **Localization:** brand names are not translated. `fa` and `en` both use `Duet HQ` / `Duet`.
5. **The character is not named Duet.** The character gets its own name, decided separately.
6. **Tests:** add a test that fails if the bare identifier `DuetHQ` (no space) appears in user-facing resources, email templates, the PWA manifest or rendered Blazor page titles — those must render `Duet HQ` / `Duet` instead.

## Brand usage

- First mention in customer-facing text: **Duet HQ**. Later mentions and in-app labels: **Duet**.
- Written as two words with a space: `Duet HQ` (not `DuetHQ`, `Duet-HQ` or `DUET HQ`) — that spaced form is reserved for display text; `DuetHQ` (no space) is the code identifier form used in namespaces, project names and database roles.
- Domain, trademark and logo are pending and managed by a human. Do not reference a domain or logo URL until provided in configuration.

## History

The project was originally scaffolded under the internal codename **Hamdel** (Persian: همدل, "empathetic"). It was renamed to `DuetHQ` in ADR-0001, before any domain/application code existed beyond the P0-01 solution skeleton, to avoid carrying two names through the rest of development.

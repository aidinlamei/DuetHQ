# Product Naming

## Decision

| Item | Value |
|---|---|
| Commercial product name | **Duet HQ** |
| Short name inside the product UI | **Duet** |
| Internal codename | **Hamdel** |

The product is marketed and presented to customers as **Duet HQ**.
`docs/ARCHITECTURE.md`, `CLAUDE.md`, `docs/IMPLEMENTATION_PLAN.md` and the codebase keep using the codename **Hamdel**. Those files are intentionally **not** renamed.

## Rules for Claude Code

1. **Code and internal names stay `Hamdel`.** Solution, projects, namespaces, database roles (`hamdel_*`), schemas, configuration sections and internal docs keep the codename. Do not rename them unless a dedicated rename task is explicitly requested.
2. **Everything a customer or employee sees uses the brand.** Page titles, emails, notifications, PWA manifest, privacy notice, dashboards, error pages and the check-in client show **Duet HQ** or **Duet**, never `Hamdel`.
3. **Never hardcode the brand name in UI or templates.** Read it from a single source:
   - configuration: `Branding:ProductName = "Duet HQ"`, `Branding:ShortName = "Duet"`
   - exposed through an `IBrandingOptions` (Options pattern) and localization placeholders such as `{ProductName}`.
4. **Localization:** brand names are not translated. `fa` and `en` both use `Duet HQ` / `Duet`.
5. **The character is not named Duet.** The character gets its own name, decided separately.
6. **Tests:** add a test that fails if the string `Hamdel` appears in user-facing resources, email templates, the PWA manifest or rendered Blazor page titles.

## Brand usage

- First mention in customer-facing text: **Duet HQ**. Later mentions and in-app labels: **Duet**.
- Written as two words with a space: `Duet HQ` (not `DuetHQ`, `Duet-HQ` or `DUET HQ`).
- Domain, trademark and logo are pending and managed by a human. Do not reference a domain or logo URL until provided in configuration.

## Future rename of the codebase

If the codebase is ever renamed from `Hamdel` to the brand, it will be a single planned task with its own ADR covering namespaces, projects, database roles and migrations. Until then, the split above is the source of truth.

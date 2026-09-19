# 0009. Application layer may reference `.Contracts`; layer dependency rules

## Status

Accepted

## Context

While implementing the architecture tests (task P1-01), the rules for what the `Application` layer may depend on turned out to contradict each other:

- `CLAUDE.md` §3.2 said `Application` "depends on Domain and `DuetHQ.Application.Abstractions` only".
- `CLAUDE.md` §3.1 and `docs/ARCHITECTURE.md` §5.1 say synchronous cross-module calls go through `<Module>.Contracts` query interfaces.
- `docs/ARCHITECTURE.md` §9.3 step 5 has `SubmitCheckInCommandHandler`, an `Application`-layer type, call `Personal.Contracts` `IPersonalEntryWriter` directly.

These cannot all hold. It is a contradiction in the documents, not an ambiguity, so per `CLAUDE.md` §1 it needs a decision, not a guess.

A second constraint shapes the rules. Layers are folders and namespaces inside **one assembly per module** (`DuetHQ.Modules.<Module>.Domain`, `.Application`, `.Infrastructure`, `.Endpoints`). Once endpoints exist, ASP.NET Core types will live inside the module assemblies, so the rules cannot be written as assembly-level bans; they have to be namespace-level and already shaped for that.

## Decision

Dependency rules per layer, inside a module (`DuetHQ.Modules.<Module>`):

| Layer | May depend on |
|---|---|
| `Domain` | `DuetHQ.SharedKernel` only |
| `Application` | `Domain`, `DuetHQ.Application.Abstractions`, `DuetHQ.SharedKernel`, and any module's `.Contracts` (its own included) |
| `Infrastructure` | everything `Application` may, plus `DuetHQ.Infrastructure.Common` and persistence/infrastructure packages |
| `Endpoints` | `Application` and `.Contracts` (plus `DuetHQ.SharedKernel` and `DuetHQ.Application.Abstractions`), and ASP.NET Core |

Always forbidden:

- `Application` (and `Domain`, `Infrastructure`, `Endpoints`) touching **another module's host assembly**, i.e. anything other than its `.Contracts` (the module boundary in `docs/ARCHITECTURE.md` §5.1).
- `Application` or `Domain` touching any `Infrastructure` or `Endpoints` namespace; `Infrastructure` touching `Endpoints` and vice versa. Dependencies point inward only.
- Banned namespace prefixes in the `.Domain` and `.Application` namespaces of every module: `Microsoft.*`, `Npgsql`, `Serilog`, `System.Data.*`. `.Domain` additionally bans async and I/O types (`System.Threading.Tasks`, `System.IO`, `System.Net`).
- **Exemptions, and the only ones:** the module root namespace (where the `<Module>Module` entry point lives, which holds the DI registration `Add<Module>(IServiceCollection, IConfiguration)`) and the `.Endpoints` namespace.

Cross-module calls **do not need adapters**. A `.Contracts` interface is already the port; the `Application` layer calls it directly.

Rules for `.Contracts` projects, which keep the above sound:

- `.Contracts` projects depend on `DuetHQ.SharedKernel` only. Cross-module contract types therefore live in `DuetHQ.SharedKernel`, including strongly typed IDs, `CohortRef` and the integration-event marker interface.
- Contracts query interfaces are plain interfaces. `IQuery<T>` and the other application abstractions are not used in `.Contracts`.

These rules are enforced by the architecture tests in `tests/DuetHQ.ArchitectureTests` (ArchUnitNET). `CLAUDE.md` §3.2 is corrected in the same change.

## Consequences

- One document set again describes the code: `CLAUDE.md` §3.2, `docs/ARCHITECTURE.md` §4 (SharedKernel contents) and the tests agree.
- Handlers such as `SubmitCheckInCommandHandler` can call `IPersonalEntryWriter` directly; no per-call adapter classes to write and keep in sync.
- Compile-time coupling between modules is visible and bounded: a module can depend on another only through `.Contracts`, and the set of allowed module-to-module edges is frozen in the tests against `docs/ARCHITECTURE.md` §5.
- `.Application` cannot use `Microsoft.Extensions.Logging` or similar directly. Cross-cutting concerns such as logging redaction stay in pipeline behaviors, as `CLAUDE.md` §4 already requires.
- Types shared across modules must be placed in `DuetHQ.SharedKernel`, which will grow deliberately and must be reviewed for anything that could carry both identity and emotional content (INV-01).
- Rules over a layer that has no code yet pass vacuously; each rule was checked against a deliberate violation.

## Alternatives considered

- **Keep `Application → Domain + Abstractions only` and add adapters.** `Application` would define its own ports and `Infrastructure` would implement them by calling `.Contracts`. Rejected: it duplicates every `.Contracts` interface with an equivalent port and a pass-through class, and adds no isolation because the `.Contracts` interface is already the boundary.
- **Allow `Application` to reference another module's `Application` or host assembly.** Rejected: it defeats module boundaries and the privacy separation (for example `Insights` reaching `Personal`, INV-05).
- **Express the layer rules at assembly level (one project per layer).** Rejected: it multiplies projects per module, contradicts the agreed solution layout (`docs/ARCHITECTURE.md` §4), and is not needed once namespace rules are checked.

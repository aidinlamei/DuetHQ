# 0004. No runtime AI; scripted level-1 conversation

## Status

Accepted

## Context

The check-in is led by a friendly character, which invites the idea of a free-form, AI-driven conversation. But the product's value rests on employees trusting that their emotional answers are handled in a bounded, explainable way (`docs/ARCHITECTURE.md` §1, §12), and its privacy model depends on the shape of the data collected.

Free text and model-based interpretation would (a) collect content that cannot be classified or bounded ahead of time, (b) make scoring non-deterministic and hard to audit, and (c) likely send emotional content to a third-party service at runtime.

## Decision

- **Level 1 conversation only.** The dialog is a scripted sequence of nodes (`Greeting`, `Question`, `NeedQuestion`, `Result`, `Closing`) with button answers, and several pre-written phrasings per node chosen at random. "Not now" is always available.
- **No free text in check-ins** (INV-11). Answers are language-neutral codes; all user-visible text is a translation key.
- **No runtime AI or LLM calls** in scoring, suppression, signals or the conversation (`CLAUDE.md` §2). Scoring (`EmotionScoringService`), cohort resolution, suppression and playbook evaluation are pure, versioned, deterministic functions with exhaustive tests.
- **The character asks, it never infers** ("How are you right now?", never "You seem stressed"), and no jokes follow low-pleasantness results.
- Content (states, needs, questions, weights, scripts, strategies) is seeded from versioned files and **reviewed by a human before publishing**; published model versions are immutable.
- Free text is allowed only in the Pilot module's feedback, which contains no emotional check-in data.

## Consequences

- Identical answers always produce identical readings, so scoring can be property-tested, explained to customers and audited.
- No emotional content leaves the system to an AI provider, which removes a whole class of privacy and vendor risks.
- Less expressive than an open conversation. The check-in gathers a small, fixed set of signals; nuance must come from well-designed questions and human-reviewed content.
- Content quality (wording, weights, strategies) is a human-owned deliverable and gates the Phase 8 checkpoint.
- Any future introduction of AI or free text would change INV-11 and needs a new ADR plus explicit human approval.

## Alternatives considered

- **LLM-driven conversational check-in.** Rejected for the MVP: unbounded content, non-deterministic scoring, and emotional data flowing to a third party contradict the privacy positioning.
- **Free-text answers classified locally.** Rejected for the MVP: still collects unbounded personal content that could identify a person or a situation, and weakens the guarantees of INV-01/INV-03.
- **Free-text answers stored only in the member's vault.** Rejected for the MVP: it adds sensitive content to the most protected store without a clear product need; can be revisited via an ADR.

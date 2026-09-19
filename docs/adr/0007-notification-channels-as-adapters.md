# 0007. Web + email + web push first; channels as adapters

## Status

Accepted

## Context

Check-ins depend on reaching people where they already are, and customers will eventually ask for Slack, Teams or a desktop companion. The MVP, however, explicitly excludes Slack/Teams and a desktop client (`docs/ARCHITECTURE.md` §1 non-goals). We need a delivery design that ships now and can grow without touching the check-in domain.

## Decision

- The MVP delivers prompts through **email (magic link)**, **web push** (VAPID), and the **web check-in itself** (Blazor WASM client with PWA manifest and service worker, entered via the link).
- Delivery goes through one port, **`ICheckInChannel`** (Strategy pattern), with `EmailChannel` and `WebPushChannel` as adapters. Email sits behind `IEmailSender`, web push behind `IWebPushSender`.
- **New channels are new implementations only.** Planned but deferred: `TeamsChannel`, `SlackChannel`, `DesktopCompanionChannel`.
- The Notifications module (`notify`) owns delivery and `WebPushSubscription`. Prompt scheduling runs per tenant timezone and slot and sends only to members without a `Participation` for that slot; web push falls back to email.
- Team broadcasts for action commitments use email in the MVP.
- Channels carry only a link/prompt. Answers are never submitted through a channel, and channels never receive emotional content.

## Consequences

- Adding Slack, Teams or a desktop client later does not change the check-in, Personal or Insights modules.
- Email magic links double as authentication for check-in entry, so token handling (hashed at rest, single use, short-lived) is security-critical (`docs/ARCHITECTURE.md` §11.4).
- Web push adds VAPID key management and subscription storage, and requires a graceful fallback to email where push is unsupported or denied.
- Prompt scheduling correctness (timezones, no duplicate prompts) needs tests with `FakeTimeProvider` (P7-02).
- Deferred channels have their own privacy questions (for example, chat platforms retaining messages); each will need review before implementation.

## Alternatives considered

- **Slack/Teams first.** Rejected for the MVP: extra integration and app-review effort, and it ties the pilot to a specific platform.
- **Email only.** Rejected: web push gives timely, low-friction nudges for a short check-in at little extra cost behind the same port.
- **Hard-coded email delivery inside the check-in module.** Rejected: it would force domain changes for every new channel and mix delivery concerns into the privacy-critical path.
- **Third-party notification platform as the core dependency.** Not chosen: it would place member contact data and prompt timing with another vendor without a pilot-scale need.

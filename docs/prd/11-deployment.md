# PRD 11 — Deployment

**Status:** Proposed · **Priority: P1 — depends on [PRD 10](10-admin-authentication.md)**

## Problem

The app only runs on the author's Mac, so it has no users. Publishing it is what turns
the project into a product. Two properties of the current design need care in a hosted
environment: state lives in **local JSON files**, and Blazor Server keeps a
**stateful WebSocket circuit** per visitor.

## Goals

- A public URL serving today's challenge over HTTPS.
- Data (teasers, stats, suggestions, issues, images) survives restarts and redeploys.
- Deploying an update is one repeatable command or a push.

## Non-goals

- Autoscaling or multi-region
- Migrating off JSON storage (see Risks)
- A custom domain (nice, not required for v1)

## Requirements

1. **Auth first.** Must not deploy publicly until [PRD 10](10-admin-authentication.md)
   ships.
2. **Persistent storage.** `App_Data/` must be on a volume that survives restarts and
   redeploys. Ephemeral container filesystems would silently discard every teaser and
   every stat — this is the single biggest deployment risk.
3. **Single instance.** The JSON stores assume one writer; the app must not be scaled
   to multiple instances without first moving to a real database.
4. **HTTPS** with automatic certificate management.
5. **Timezone independence.** Already handled — `AppTime` pins day boundaries to
   Pacific regardless of server locale — but must be verified on the host, since a
   server running UTC is the exact case this protects against.
6. **Backups.** A scheduled copy of `App_Data/` off the host. Restoring must be
   documented and tested at least once.
7. **Logging** sufficient to notice unhandled exceptions.
8. **Reasonable WebSocket support** for Blazor Server circuits (rules out hosts that
   only serve static content or short-lived functions).

## Candidate hosts

| Host | Fit |
|---|---|
| **Fly.io** | Good — persistent volumes, cheap, WebSockets fine |
| **Azure App Service** | Good — first-class .NET, Easy Auth available for PRD 10 |
| **A small VPS** | Most control, most maintenance |
| **Raspberry Pi at home** | Cheapest; needs tunnelling and has home-uptime caveats |

## Acceptance criteria

- [ ] Public HTTPS URL serves the current challenge
- [ ] `/admin` reachable only with the passphrase
- [ ] Adding a teaser, then redeploying, retains it (persistence proven, not assumed)
- [ ] Day rollover verified correct on a UTC-clock server
- [ ] A backup has been taken **and restored** once
- [ ] Documented deploy command in the README

## Risks

- **Data loss from ephemeral storage** — the top risk; verify the volume before
  trusting it with content.
- **Concurrency.** JSON writes are lock-guarded in-process only. One instance is fine
  for hundreds of daily solvers; beyond that, or if scaling is ever needed, swap
  `TeaserStore`/`StatsStore` for SQLite. That change is contained because all access
  already funnels through those services.
- **Cold starts** on scale-to-zero hosting would drop Blazor circuits; prefer an
  always-on instance.

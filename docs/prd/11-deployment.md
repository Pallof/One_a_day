# PRD 11 — Deployment

**Status:** Proposed · **Priority: P1 — depends on [PRD 10](10-admin-authentication.md)**

## Problem

The app only runs on the author's Mac, so it has no users. Publishing it is what turns
the project into a product. Two properties of the current design need care in a hosted
environment: state lives in **local JSON files**, and Blazor Server keeps a
**stateful WebSocket circuit** per visitor.

## Goals

- A public URL serving today's challenge over HTTPS.
- Data (teasers, stats, suggestions, issues, **rotation**, images) survives restarts
  and redeploys.
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

   This includes **`rotation.json`**, which is easy to mistake for a cache. It is not:
   it holds which teaser ran on which day. Losing it makes already-published days
   re-resolvable, so a past day can come back with a *different* teaser — retroactively
   changing what "yesterday's solution" was ([PRD 08](08-recycling-rotation.md)).

3. **Behind a proxy, forwarded headers are mandatory.** `RemoteIpAddress` is whoever
   connected to Kestrel; behind nginx, Cloudflare, or Azure App Service that is **the
   proxy**, which collapses every visitor into one IP. The suggestion form's
   three-per-IP cap would then close the form for the entire internet after three
   submissions, every day. Configure `UseForwardedHeaders` and verify a real client IP
   arrives before going public. Deferred here by [PRD 06](06-community-feedback.md).

4. **Persist the Data Protection keys.** They default to a local folder that does not
   survive a container restart. Lose them and every `ProtectedLocalStorage` value
   becomes undecryptable: each visitor looks brand new, unique-attempter counts
   inflate, and the one-suggestion-per-day limit resets for everyone. It fails
   *silently* — the app keeps working — so it will not be noticed without checking.

5. **Single instance.** The JSON stores assume one writer; the app must not be scaled
   to multiple instances without first moving to a real database.
6. **HTTPS** with automatic certificate management.
7. **Timezone independence.** Already handled — `AppTime` pins day boundaries to
   Pacific regardless of server locale — but must be verified on the host, since a
   server running UTC is the exact case this protects against.
8. **Backups.** A scheduled copy of `App_Data/` off the host. Restoring must be
   documented and tested at least once.
9. **Logging** sufficient to notice unhandled exceptions.
10. **Reasonable WebSocket support** for Blazor Server circuits (rules out hosts that
    only serve static content or short-lived functions).
11. **Self-host the webfonts.** The app currently loads Fraunces and Atkinson
    Hyperlegible from `fonts.googleapis.com`, which puts a third party on the render
    path and exposes visitor IPs to Google. Serving the woff2 files from `wwwroot`
    removes both ([PRD 09](09-visual-design.md)).

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
- [ ] **`rotation.json` survives a redeploy** — check a past day still resolves to the
      same teaser afterwards, not merely that the file exists
- [ ] **A real client IP reaches the app**, not the proxy's (submit twice from two
      devices and confirm they are counted separately)
- [ ] Data Protection keys persist across a restart — a visitor is not treated as new
- [ ] Day rollover verified correct on a UTC-clock server
- [ ] A backup has been taken **and restored** once
- [ ] Documented deploy command in the README

## Risks

- **Data loss from ephemeral storage** — the top risk; verify the volume before
  trusting it with content.
- ~~**Torn writes.**~~ **Fixed 2026-09-09.** Every store persisted with
  `File.WriteAllText`, which truncates the file *then* writes — a crash inside that
  window left a file that no longer parses, losing the whole store rather than the last
  record. All five now go through `Services/AtomicFile.cs`: write a sibling temp file,
  then `File.Move(..., overwrite: true)`, which is an atomic rename within one
  filesystem.

  Measured before and after, killing the process mid-write on a 21 MB file, 20 trials
  each, starting from a valid file: **`File.WriteAllText` left the file unreadable
  18 times out of 20; the atomic version 0 out of 20.**

  Two properties to preserve if this is ever touched. The temp file must stay a
  **sibling** of the target — a rename is only atomic within one filesystem, and using
  the system temp directory silently degrades it to copy-then-delete across a mount
  boundary. And `flushToDisk` remains **opt-in**: the rename alone survives process
  crash, OOM, restart and deploy, while forcing a disk sync additionally survives power
  loss at the cost of a real fsync on every write — which `StatsStore` performs on every
  answer submitted.

- **A failed write still throws into the UI.** Atomic writes protect the *data*, not the
  caller: a full disk or a permissions error propagates out of `Persist()`, through
  `TryAdd`, and out of a component's `Submit()`, which kills that visitor's SignalR
  circuit. The store is intact and nothing is lost, but the visitor sees a broken page
  rather than a message. Narrower than it was, and still open.
- **Concurrency.** JSON writes are lock-guarded in-process only. One instance is fine
  for hundreds of daily solvers; beyond that, or if scaling is ever needed, swap
  `TeaserStore`/`StatsStore` for SQLite. That change is contained because all access
  already funnels through those services.
- **Cold starts** on scale-to-zero hosting would drop Blazor circuits; prefer an
  always-on instance.

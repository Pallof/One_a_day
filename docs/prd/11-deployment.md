# PRD 11 — Deployment

**Status:** Proposed · **Priority: P1 — the remaining launch blocker**

## Problem

The app only runs on the author's Mac, so it has no users. Publishing it is what turns
the project into a product. Two properties of the current design need care in a hosted
environment: state lives in **local JSON files**, and Blazor Server keeps a
**stateful WebSocket circuit** per visitor.

## Goals

- A public URL serving today's challenge over HTTPS.
- Data (teasers, stats, suggestions, issues, **rotation**, subscribers, images)
  survives restarts and redeploys.
- Deploying an update is one repeatable command or a push.

## Non-goals

- Autoscaling or multi-region
- Migrating off JSON storage (see Risks)
- A custom domain (nice, not required for v1)

## Requirements

1. **Run in Production — never Development.** The live site has no admin page only
   because it doesn't run in Development ([PRD 10](10-admin-authentication.md)). The app
   now enforces this itself: it refuses to start in Development unless it's a Debug build
   on a machine marked as the author's computer. Deploy a **published** build — Release,
   so the fence holds even if the host sets `ASPNETCORE_ENVIRONMENT=Development` — and
   **never mark the server** as the author's machine. Most hosts default to Production;
   confirm it anyway.
2. **Persistent storage.** `App_Data/` must be on a volume that survives restarts and
   redeploys. Ephemeral container filesystems would silently discard every teaser and
   every stat — this is the single biggest deployment risk.

   This includes **`rotation.json`**, which is easy to mistake for a cache. It is not:
   it holds which teaser ran on which day. Losing it makes already-published days
   re-resolvable, so a past day can come back with a *different* teaser — retroactively
   changing what "yesterday's solution" was ([PRD 08](08-recycling-rotation.md)).

3. **Behind a proxy, forwarded headers are mandatory.** ✅ **Built 2026-09-16** —
   `Services/ProxyOptions.cs`. Set `Proxy:Enabled` and name a trusted source on the host;
   the app refuses to start if one is announced without the other.

   Two separate failures, one cause. `RemoteIpAddress` is whoever connected to Kestrel;
   behind nginx, Cloudflare, or Azure App Service that is **the proxy**, which collapses
   every visitor into one IP, so the suggestion form's three-per-IP cap closes the form for
   the entire internet after three submissions, every day. **And the site does not load at
   all**: the proxy terminates TLS and forwards plain HTTP, so `UseHttpsRedirection`
   redirects to HTTPS, the proxy forwards plain HTTP again, and the browser gives up with
   `ERR_TOO_MANY_REDIRECTS`. The redirect loop is the more severe of the two and was *not*
   previously recorded here — found 2026-09-16 while reading the pipeline.

   `UseProxyHeaders()` therefore runs **first in the pipeline**, above `UseHsts` and
   `UseHttpsRedirection`, both of which read the scheme. Placed below them the loop returns
   while the code still looks correct.

   Still to verify on the real host: that a genuine client IP arrives (see acceptance
   criteria). Prefer `KnownProxies`/`KnownNetworks` over `TrustAllProxies` wherever the host
   documents a fixed address — trusting every sender lets a client name its own address and
   step past the cap, which [PRD 06](06-community-feedback.md) already accepts as reachable
   by other means, but there's no reason to make it free.

4. **Persist the Data Protection keys.** ✅ **Built 2026-09-16** — persisted to
   `App_Data/keys/`, which is the volume requirement 2 already covers and `.gitignore`
   already excludes.

   They default to a local folder that does not survive a container restart. Lose them and
   every `ProtectedLocalStorage` value becomes undecryptable: each visitor looks brand new,
   unique-attempter counts inflate, and the one-suggestion-per-day limit resets for
   everyone.

   > **Correction, found 2026-09-16 while verifying this.** An earlier draft of this
   > requirement said the failure was *silent* — that the app keeps working and only the
   > numbers drift. That was wrong, and the mistake mattered.
   > `ProtectedLocalStorage.GetAsync` does **not** catch decryption failures: it throws a
   > `CryptographicException` out of `OnAfterRenderAsync`, which is unhandled, which **kills
   > the visitor's circuit**. Their page stops working. Moving the key ring made this
   > immediately observable — every browser still holding a value from the old default ring
   > logged `The key {…} was not found in the key ring` and lost its circuit.
   >
   > Reading now goes through `ProtectedStorageExtensions.ReadOrDefaultAsync`, which treats
   > an unreadable value as no value: a new id is minted and the stale value is overwritten
   > on the next write. Only with that in place is the *silent* description accurate — and
   > it is the behaviour any future key rotation, restore-from-backup, or ring move depends
   > on.

   The application name is **pinned** to `OneADay` rather than left to default. The default
   discriminator is the assembly name, so renaming the project would silently invalidate
   every stored value and reset every visitor. This is what makes the pending rename safe.

   Note for backups (requirement 8): `App_Data/keys/` is unencrypted at rest, so it inherits
   the same "as private as the host" rule the subscriber list already carries.

5. **Single instance.** The JSON stores assume one writer; the app must not be scaled
   to multiple instances without first moving to a real database.
6. **HTTPS** with automatic certificate management.
7. **Timezone independence.** Already handled — `AppTime` pins day boundaries to
   Pacific regardless of server locale — but must be verified on the host, since a
   server running UTC is the exact case this protects against.
8. **Backups.** A scheduled copy of `App_Data/` off the host. Restoring must be
   documented and tested at least once. The folder holds subscriber email addresses
   ([PRD 15](15-email-subscriptions.md)), so the copy must be as private as the host.
9. **Logging** sufficient to notice unhandled exceptions.
10. **Reasonable WebSocket support** for Blazor Server circuits (rules out hosts that
    only serve static content or short-lived functions).
11. **Self-host the webfonts.** The app currently loads Lora and Atkinson Hyperlegible
    from `fonts.googleapis.com`, which puts a third party on the render path and
    exposes visitor IPs to Google. Serving the woff2 files from `wwwroot` removes both
    ([PRD 09](09-visual-design.md)).
12. **Email settings.** `Email__AppPassword` as a secret ([PRD 14](14-email-notifications.md)),
    and **`Site__BaseUrl` set to the public HTTPS address** ([PRD 15](15-email-subscriptions.md)).
    The second is easy to miss and fails silently: every link in every email — confirm,
    unsubscribe, solve — is built from it, and the default points at `localhost`.

13. **Privacy and proxy settings.** Two more, both added 2026-09-16:

    | Setting | Required? | If it's wrong |
    |---|---|---|
    | `Privacy__IpHashKey` | **Yes** — a secret, ≥32 chars | The app **refuses to start** |
    | `Proxy__Enabled` + a trusted source | Yes, behind a proxy | Refuses to start if one is set without the other |

    Unlike every other setting here, a missing `Privacy__IpHashKey` **stops the deploy**
    rather than degrading quietly — deliberately, because the quiet version stores
    recoverable visitor addresses while looking fine ([PRD 06](06-community-feedback.md)).
    Generate it once and keep it: rotating it resets everyone's daily suggestion cap for
    the rest of that day.

    ```bash
    openssl rand -base64 32
    ```

    For the proxy, set either `Proxy__KnownProxies__0` / `Proxy__KnownNetworks__0` (preferred,
    where the host documents a fixed address) or `Proxy__TrustAllProxies=true`.
13. **A publish command for teasers.** One command copies `teasers.json` and any new
    images into the server's `App_Data/` and restarts the app — **never the whole
    folder**, which would overwrite live stats, rotation history and subscribers
    ([PRD 10](10-admin-authentication.md)). The bank must be in place before the first
    start: the live site refuses to start without one. `dotnet publish` never includes
    `App_Data/` — the project file excludes it, because by default it would ship every
    future answer and real subscriber address with each deploy.

## Candidate hosts

| Host | Fit |
|---|---|
| **Fly.io** | Good — persistent volumes, cheap, WebSockets fine |
| **Azure App Service** | Good — first-class .NET |
| **A small VPS** | Most control, most maintenance |
| **Raspberry Pi at home** | Cheapest; needs tunnelling and has home-uptime caveats |

## Acceptance criteria

- [ ] Public HTTPS URL serves the current challenge
- [ ] `/admin` shows the not-found page on the live site — typed into the address bar and
      clicked through from inside the app
- [ ] Publishing a teaser reaches the live site without touching its stats, rotation or
      subscribers
- [ ] Adding a teaser, then redeploying, retains it (persistence proven, not assumed)
- [ ] **`rotation.json` survives a redeploy** — check a past day still resolves to the
      same teaser afterwards, not merely that the file exists
- [ ] **A real client IP reaches the app**, not the proxy's (submit twice from two
      devices and confirm they are counted separately)
- [ ] Data Protection keys persist across a restart — a visitor is not treated as new
- [ ] Day rollover verified correct on a UTC-clock server
- [ ] A backup has been taken **and restored** once
- [ ] The links in a real email open the public site — `Site__BaseUrl` is set
- [ ] Documented deploy command in the README

## Risks

- **Data loss from ephemeral storage** — the top risk; verify the volume before
  trusting it with content.
- ~~**Torn writes.**~~ **Fixed 2026-09-09.** Every store persisted with
  `File.WriteAllText`, which truncates the file *then* writes — a crash inside that
  window left a file that no longer parses, losing the whole store rather than the last
  record. Every store now goes through `Services/AtomicFile.cs`: write a sibling temp file,
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
  always-on instance. It is required, not preferred, for the daily email: an app
  asleep at 7am sends nothing until a visitor wakes it, and a day with no visitor is
  skipped entirely ([PRD 15](15-email-subscriptions.md)).

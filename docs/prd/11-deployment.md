# PRD 11 — Deployment

**Status:** Proposed · **Priority: P1 — the remaining launch blocker**

## In plain terms

The site runs only on the author's Mac, so nobody can use it. Putting it online is what turns
this from a project into a product. Two things about how it's built need care on a real server:
**all the data lives in plain files on disk**, so the server needs storage that survives a
restart; and **every visitor holds an open connection**, so it needs a host that allows those
and never goes to sleep.

The single biggest risk is storage. On many hosts the disk is wiped on every deploy — which
would silently destroy every puzzle, every statistic and every subscriber.

## Goals

- A public HTTPS address serving today's challenge at `stumpty.com`.
- Data (teasers, stats, suggestions, issues, **rotation**, subscribers, images) survives
  restarts and redeploys.
- Deploying an update is one repeatable command or a push.

## Non-goals

- Autoscaling or multi-region
- Migrating off file storage (see Risks)

## Requirements

1. **Run in Production — never Development.** The live site has no admin page only because it
   doesn't run in Development ([PRD 10](10-admin-authentication.md)). The app enforces this
   itself: it refuses to start in Development unless it's a Debug build on a machine marked as
   the author's. Deploy a **published** build, and **never mark the server**. Most hosts default
   to Production; confirm it anyway.

2. **Persistent storage.** `App_Data/` must be on a volume that survives restarts and redeploys.
   Ephemeral container filesystems would silently discard everything — **the single biggest
   deployment risk.**

   This includes **`rotation.json`**, which is easy to mistake for a cache. It isn't: it records
   which teaser ran on which day. Losing it lets an already-published day resolve to a
   *different* teaser, retroactively changing what "yesterday's solution" was
   ([PRD 08](08-recycling-rotation.md)).

3. **Behind a proxy, forwarded headers are mandatory.** ✅ **Built 2026-09-16.** Set
   `Proxy:Enabled` and name a trusted source; the app refuses to start if one is set without the
   other.

   Two failures, one cause. Without them every visitor collapses into the proxy's address, so
   the three-per-IP cap closes the suggestion form for the entire internet after three
   submissions a day. **And the site doesn't load at all:** the proxy handles the secure
   connection and passes an insecure one inward, the app redirects to the secure address, the
   proxy passes insecure again, and the browser gives up. The redirect loop is the more severe
   of the two and was *not* previously recorded here — found 2026-09-16 while reading the code.

   `UseProxyHeaders()` therefore runs **first**, above anything that reads the connection
   scheme. Placed below, the loop returns while the code still looks correct. Prefer naming a
   fixed proxy address over trusting every sender wherever the host documents one.

4. **Persist the Data Protection keys.** ✅ **Built 2026-09-16** — kept in `App_Data/keys/`,
   which requirement 2 already covers and `.gitignore` already excludes.

   These keys encrypt the anonymous visitor id in each browser. They default to a folder that
   doesn't survive a restart, and losing them makes every stored id unreadable: visitors look
   brand new, attempter counts inflate, and daily limits reset for everyone.

   > **A correction worth keeping.** An earlier draft of this requirement said the failure was
   > *silent*. It wasn't — reading an unreadable value **threw**, which killed the visitor's
   > connection and broke their page. Moving the key ring made this visible immediately.
   > Reading now goes through `ProtectedStorageExtensions.ReadOrDefaultAsync`, which treats an
   > unreadable value as no value. Only *with that in place* is "silent" accurate — and every
   > future key rotation or restore-from-backup depends on it.

   The application name is **pinned** rather than left to default, because the default is the
   project name — so renaming the project would silently invalidate every stored id. This is
   what makes the Stumpty rename safe.

   `App_Data/keys/` is unencrypted at rest, so it inherits the same "as private as the host"
   rule as the subscriber list (requirement 8).

5. **Single instance.** The file stores assume one writer; don't scale to multiple instances
   without first moving to a real database.
6. **HTTPS** with automatic certificate management.
7. **Timezone independence.** Already handled — day boundaries are pinned to Pacific regardless
   of server locale — but verify on the host, since a server running UTC is the exact case this
   protects against.
8. **Backups.** A scheduled copy of `App_Data/` off the host, with a restore **tested at least
   once**. It holds subscriber email addresses and the encryption keys, so the copy must be as
   private as the host.
9. **Logging** sufficient to notice unhandled exceptions.
10. **Support for long-lived connections** — rules out hosts that only serve static content or
    short-lived functions.
11. **Self-host the webfonts.** The app loads Lora and Atkinson Hyperlegible from Google, which
    puts a third party on the render path and exposes visitor IPs to them. Serving the files
    from `wwwroot` removes both ([PRD 09](09-visual-design.md)).

12. **Settings the deploy needs.** Four, and two of them stop the app starting:

    | Setting | Required | If it's wrong |
    |---|---|---|
    | `Privacy__IpHashKey` | **Yes** — a secret, ≥32 chars | **Refuses to start** |
    | `Proxy__Enabled` + a trusted source | Behind a proxy | **Refuses to start** if one is set without the other |
    | `Email__AppPassword` | For any email | No mail; site unaffected ([PRD 14](14-email-notifications.md)) |
    | `Site__BaseUrl` = `https://stumpty.com` | Yes | **Fails silently** — every link in every email is built from it and defaults to localhost ([PRD 15](15-email-subscriptions.md)) |

    The refusals are deliberate. A missing hash key quietly stores recoverable visitor
    addresses while looking fine ([PRD 06](06-community-feedback.md)), so stopping the deploy is
    the lesser harm. Generate the key once and **keep it** — rotating it resets everyone's daily
    suggestion cap for the rest of that day:

    ```bash
    openssl rand -base64 32
    ```

13. **A publish command for teasers.** One command copies `teasers.json` and any new images into
    the server's `App_Data/` and restarts — **never the whole folder**, which would overwrite
    live stats, rotation history and subscribers ([PRD 10](10-admin-authentication.md)). The bank
    must be in place before the first start, because the live site refuses to start without one.
    `dotnet publish` never includes `App_Data/`; the project file excludes it, since by default
    it would ship every future answer and real subscriber address on every deploy.

## Candidate hosts

| Host | Fit | Cost |
|---|---|---|
| **Fly.io** | Best fit — persistent volumes, long connections, always-on | ~$4/month (512MB + 1GB volume) |
| **Azure App Service** | First-class .NET, least friction | ~$13/month (B1) |
| **A small VPS** | Most control, most maintenance — and the only option where the proxy is on the same machine, so it can be named exactly | ~$5–6/month |
| **Raspberry Pi at home** | Cheapest; needs tunnelling, and home uptime caveats | hardware |

On any platform host, **switch off scale-to-zero** — see the cold-start risk below.

## Acceptance criteria

- [ ] Public HTTPS address serves the current challenge
- [ ] `/admin` shows not-found on the live site — typed in, *and* clicked through from inside
      the app
- [ ] Publishing a teaser reaches the site without touching its stats, rotation or subscribers
- [ ] Adding a teaser, then redeploying, retains it — persistence proven, not assumed
- [ ] **`rotation.json` survives a redeploy** — check a past day still resolves to the same
      teaser, not merely that the file exists
- [ ] **A real client IP reaches the app**, not the proxy's — submit from two devices and
      confirm they count separately
- [ ] Data Protection keys persist across a restart — a visitor isn't treated as new
- [ ] Day rollover correct on a UTC-clock server
- [ ] A backup has been taken **and restored** once
- [ ] Links in a real email open the public site
- [ ] Deploy command documented in the README

## Risks

- **Data loss from ephemeral storage** — the top risk. Verify the volume before trusting it
  with content.
- **A failed write still throws into the UI.** Atomic writes protect the *data*, not the caller:
  a full disk or permissions error propagates out and kills that visitor's connection. The store
  is intact and nothing is lost, but the visitor sees a broken page rather than a message.
  Narrower than it was, still open.
- **Concurrency.** File writes are guarded within one process only. One instance is fine for
  hundreds of daily solvers; beyond that, swap the stores for SQLite. Contained, because all
  access already funnels through them.
- **Cold starts** on scale-to-zero hosting drop connections — and it's *required*, not
  preferred, for the daily email: an app asleep at 7am sends nothing until a visitor wakes it,
  and a day with no visitor is skipped entirely ([PRD 15](15-email-subscriptions.md)).
- ~~**Torn writes.**~~ **Fixed 2026-09-09.** Stores used to write by truncating the file *then*
  writing, so a crash inside that window left a file that no longer parsed — losing the whole
  store rather than the last record. Every store now writes to a temporary file beside the
  target and renames it, which the filesystem does atomically.

  Measured by killing the process mid-write on a 21 MB file, 20 trials each: **the old way left
  the file unreadable 18 times out of 20; the new way 0 out of 20.**

  Two properties to preserve if this is ever touched. The temporary file must stay **beside**
  the target — a rename is only atomic within one filesystem, and using the system temp
  directory silently degrades it to copy-then-delete. And forcing a disk sync stays
  **opt-in**: the rename alone survives a crash, restart or deploy, while a sync additionally
  survives power loss at the cost of real disk I/O on every write.

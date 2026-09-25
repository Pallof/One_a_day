# PRD 11 — Deployment

**Status:** Proposed · **Priority: P1 — the remaining launch blocker**

## In plain terms

The site runs only on the author's Mac, so nobody can use it; putting it online turns a project into
a product. Two things about how it's built need care on a real server: **all the data lives in plain
files on disk**, so the server needs storage that survives a restart; and **every visitor holds an
open connection**, so the host must allow those and never go to sleep.

The biggest risk is storage: on many hosts the disk is wiped on every deploy, which would silently
destroy every puzzle, statistic and subscriber.

## Goals

- `stumpty.com` serves today's challenge over HTTPS (a secure address).
- All data — teasers, statistics, suggestions, reports, **rotation**, subscribers, images — survives
  restarts and redeploys.
- Deploying an update is one repeatable command, or a push.

## Non-goals

- Automatic scaling or multiple regions
- Moving off file storage (see Risks)

## Requirements

1. **Run in Production, never Development.** The live site has no admin page only because it doesn't
   run in Development ([PRD 10](10-admin-authentication.md)), and the app enforces this itself.
   Deploy a **published** build and **never mark the server** as the author's. Most hosts default to
   Production; check anyway.

2. **Storage that survives.** `App_Data/` must be on a disk that survives restarts and redeploys; a
   host that wipes it would silently throw everything away — **the biggest deployment risk.** That
   includes `rotation.json`, easy to mistake for a cache but in fact the record of which teaser ran
   on which day ([PRD 08](08-recycling-rotation.md)).

3. **Behind a proxy, read its forwarded headers.** ✅ **Built 2026-09-16.** A proxy sits in front of
   the app, handles the secure connection, and passes each visitor through with a note (a
   "forwarded header") saying who they really are. Set `Proxy:Enabled` and name a trusted source;
   the app refuses to start with one but not the other (`Services/ProxyOptions.cs`).

   Ignoring the note causes two failures. Every visitor appears as the proxy, so the
   three-per-address limit closes the suggestion form for everyone after three suggestions a day.
   **And the site doesn't load at all:** the app keeps redirecting to the secure address while the
   proxy keeps passing visits on as insecure, until the browser gives up. *History: that loop, the
   worse failure, wasn't recorded here until it was found by reading the code on 2026-09-16.*

   So `UseProxyHeaders()` runs before anything that checks for a secure connection, after only
   the Cloudflare lock and the traffic count (requirement 14). Placed later, the loop returns while the code still looks
   right. Where the host documents a fixed proxy address, name it rather than trusting every sender.

   With Cloudflare in front of Fly, a visitor arrives through **two** proxies, so
   `Proxy__ForwardLimit` must be 2 or every visitor looks like a Cloudflare address. The lock makes
   trusting every sender safe here: nothing reaches the app without passing through both. Confirm
   it with the two-device check below.

4. **Keep the encryption keys.** ✅ **Built 2026-09-16** — in `App_Data/keys/`, which requirement 2
   already covers and the repository already excludes.

   These keys encrypt the small things the site keeps in each browser: the anonymous visitor ID the
   suggestion form uses for its one-a-day limit, and whether the Twenty Four panel was closed. By
   default the keys don't survive a restart, and losing them makes all of it unreadable — the daily
   suggestion limit resets for everyone, and the panel comes back.

   > **History — a correction:** an earlier draft called this failure *silent*. It wasn't — reading
   > an unreadable ID **crashed** the visitor's connection and broke their page, as moving the keys
   > showed at once. Reading now goes through `ProtectedStorageExtensions.ReadOrDefaultAsync`, which
   > treats an unreadable value as none; only with that is "silent" true, and every future key
   > change or restore from backup relies on it.

   The name the keys are tied to is **pinned** (`OneADay`) rather than following the project's name,
   which would invalidate every stored ID on a rename — this is what made the Stumpty rename safe.
   `App_Data/keys/` isn't encrypted on disk, so like the subscriber list it must stay as private as
   the host (requirement 8).

5. **One copy of the app.** File storage assumes a single writer; don't run several copies without
   first moving to a real database.
6. **HTTPS**, with certificates renewed automatically.
7. **Time zone.** Already handled — days are pinned to Pacific whatever the server's clock — but
   check on the host, since a server on UTC is exactly the case this guards against.
8. **Backups.** A scheduled copy of `App_Data/` off the host, with a restore **tested at least
   once**. It holds subscriber addresses and the encryption keys, so the copy must be as private as
   the host.
9. **Logging** good enough to notice unexpected errors.
10. **Long-lived connections allowed** — ruling out hosts that only serve fixed files or short-lived
    functions.
11. **Serve the fonts ourselves.** Loading Lora and Atkinson Hyperlegible from Google puts a third
    party on every page load and shows it visitors' IP addresses; serving them from `wwwroot`
    removes both ([PRD 09](09-visual-design.md)).
12. **Settings the deploy needs.** Five; three of them stop the app starting:

    | Setting | Required | If it's wrong |
    |---|---|---|
    | `Privacy__IpHashKey` | **Yes** — a secret, at least 32 characters | **Refuses to start** |
    | `CloudflareLock__Secret` | **Yes** — a secret, at least 32 characters, the same value as Cloudflare's header rule | **Refuses to start**, unless `CloudflareLock__Enabled` is false (requirement 14) |
    | `Proxy__Enabled` + a trusted source | Behind a proxy | **Refuses to start** if one is set without the other |
    | `Email__AppPassword` | For any email | No mail; the site is unaffected ([PRD 14](14-email-notifications.md)) |
    | `Site__BaseUrl` = `https://stumpty.com` | Yes | **Fails silently** — every link in every email is built from it, and it defaults to a local address ([PRD 15](15-email-subscriptions.md)) |

    The refusals are deliberate: without the key the site would quietly store recoverable visitor
    addresses while looking fine ([PRD 06](06-community-feedback.md)), so stopping the deploy is the
    lesser harm. Generate the key once and **keep it** — changing it resets everyone's daily
    suggestion limit for the rest of that day:

    ```bash
    openssl rand -base64 32
    ```

13. **A publish command for teasers.** One command copies `teasers.json` and any new images into the
    server's `App_Data/` and restarts — **never the whole folder**, which would overwrite live
    statistics, rotation history and subscribers ([PRD 10](10-admin-authentication.md)). The bank
    must be in place before the first start, because the live site won't start without one. The
    build itself already leaves `App_Data/` out (PRD 10).

14. **Only answer Cloudflare.** ✅ **Built 2026-09-24.** Cloudflare's rate limit and cache only cover
    traffic that goes through it, but every Fly app also answers at its own `.fly.dev` address. Fly
    bills $0.02 per GB sent, and a bot picks the biggest file it can find. Measured on the published
    build, one machine re-downloading the 196 KB Blazor script that way could cost about $1,100 a
    month. So Cloudflare stamps a secret header on every request it forwards, and the app turns away
    anything without it before any other code runs (`Services/CloudflareLock.cs`). The refusal is an
    empty 403 of 99 bytes, so a nonstop flood costs about $2 a month.

    - **Cloudflare:** Rules → Create rule → Request Header Transform Rule, for all incoming
      requests: **Set static** `X-Origin-Verify` to the secret. "Set static" overwrites any value a
      visitor sends.
    - **Host:** `CloudflareLock__Secret`, the same value, made with the `openssl` command above.
      To change it, change both at once; until they match, every visitor gets the 403.
    - **On unless switched off.** A production start without the secret refuses. A local production
      rehearsal sets `CloudflareLock__Enabled=false`.
    - **Health checks** must be TCP checks or send the header.
    - **The airtight version** is a Cloudflare Tunnel, which gives the server no public address at
      all. That's a deploy-time choice, and the lock stays useful behind it.

    Two free Cloudflare settings go with it:
    - **Rate limiting:** more than 50 requests in 10 seconds from one address blocks it for 10
      seconds. A real first visit makes about 10. This caps one attacker loading pages at about
      $1.70 a month.
    - **Caching level: Ignore query string.** Otherwise `?x=123` on a file's address skips the
      cache and reaches the server every time.

    Fly has no billing alerts or spending caps, so the site sends its own. The first time a day
    passes 50,000 requests, the author gets one email ([PRD 14](14-email-notifications.md)). Raise
    `TrafficAlert__DailyThreshold` as the site grows.

## Candidate hosts

| Host | Fit | Cost |
|---|---|---|
| **Fly.io** | Best fit — lasting storage, long connections, always on | ~$4/month (512MB + 1GB volume) |
| **Azure App Service** | First-class .NET, least friction | ~$13/month (B1) |
| **A small rented server (VPS)** | Most control, most upkeep — and the only option where the proxy is on the same machine, so it can be named exactly | ~$5–6/month |
| **Raspberry Pi at home** | Cheapest; needs a tunnel, and home uptime caveats | hardware |

On any platform host, **switch off sleeping when idle** ("scale to zero") — see Risks.

## Acceptance criteria

- [ ] Public HTTPS address serves the current challenge
- [ ] `/admin` shows not-found on the live site — typed in, *and* clicked through from inside the app
- [ ] Publishing a teaser reaches the site without touching its stats, rotation or subscribers
- [ ] Adding a teaser, then redeploying, retains it — persistence proven, not assumed
- [ ] **`rotation.json` survives a redeploy** — check a past day still resolves to the same teaser,
      not merely that the file exists
- [ ] **A real client IP reaches the app**, not the proxy's — submit from two devices and confirm
      they count separately
- [ ] Data Protection keys persist across a restart — a visitor isn't treated as new
- [ ] Day rollover correct on a UTC-clock server
- [ ] A backup has been taken **and restored** once
- [ ] Links in a real email open the public site
- [ ] Deploy command documented in the README
- [ ] The server's own `.fly.dev` address gets an empty 403, while stumpty.com loads

### The Cloudflare-only lock — `CloudflareLockTests`

- [x] Without the header, or with anything but the exact secret (a prefix, extra characters, other
      letter case, a second value alongside it), a request gets an empty 403; the exact secret gets
      the page
- [x] With no settings at all the lock is on and refuses to start; a missing or short secret refuses
- [x] Proven against the real files: the lock is the first thing in Program.cs's pipeline, and
      appsettings.json switches it on while holding no secret
- [x] Mutation-verified: six breaks, each caught — letting a missing header through, accepting any
      value, moving the lock below the static files, defaulting it off, accepting a short secret, and
      switching it off in the production settings

## Risks

- **Data lost to a wiped disk** — the top risk. Prove the storage survives before trusting it with
  content.
- **A failed save still breaks the page.** Safe saving (below) protects the *data*, not the visitor:
  a full disk or permissions error still kills that visitor's connection. Nothing is lost, but they
  see a broken page instead of a message. Narrower than it was; still open.
- **Saves at the same moment** are only coordinated within one running copy. One copy is fine for
  hundreds of daily solvers; beyond that, move the stores to SQLite — a contained change, since all
  saving goes through them.
- **Hosts that sleep when idle** drop visitors' connections — and for the daily email, staying awake
  is *required*: an app asleep at 7am sends nothing until a visitor wakes it, and a day with no
  visitor is skipped entirely ([PRD 15](15-email-subscriptions.md)).
- ~~**Half-written files.**~~ **Fixed 2026-09-09.** Saves used to empty the file *then* write it, so a
  crash in between left a file that no longer loaded — losing the whole store, not just the last
  change. Every save now writes a temporary file beside the real one and swaps it in, which the file
  system does in one step. Killing the app mid-save on a 21 MB file, 20 times each: **the old way
  left the file unreadable 18 times out of 20; the new way 0.** Keep two things if this is touched:
  the temporary file stays **beside** the real one (the swap is one step only within the same disk;
  the system's temp folder quietly turns it into copy-then-delete), and forcing data onto the
  physical disk stays **optional** (the swap alone survives a crash, restart or deploy; forcing also
  survives a power cut, at the cost of real disk work on every save).

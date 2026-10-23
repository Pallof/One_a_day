# PRD 11 — Deployment

**Status:** Spec of record · live at https://stumpty.com

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
   on which day ([PRD 08](08-recycling-rotation.md)), and `weekly-summary.json`, without which a
   redeploy could send the author's Monday email twice ([PRD 16](16-site-metrics.md)).

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
   first moving to a real database. On Fly, deploy with `fly deploy --ha=false`, or it adds a spare.
6. **HTTPS**, with certificates renewed automatically.
7. **Time zone.** Already handled — days are pinned to Pacific whatever the server's clock — but
   check on the host, since a server on UTC is exactly the case this guards against.
8. **Backups.** ✅ **In place, and a restore tested, 2026-10-15.** A scheduled copy of `App_Data/`
   off the host, with a restore **tested at least once**. It holds subscriber addresses and the
   encryption keys, so the copy must be as private as the host.

   **What's backed up, and how often.** Fly snapshots the whole disk (`stumpty_data`) every day at
   about 04:50 UTC, late evening Pacific, and keeps **30 days** of them. Fly's default is 5. The live
   disk was raised to 30 with `fly volumes update` on 2026-10-15; fly.toml records it for any disk a
   deploy creates, and `DeployFilesTests` pins it. Snapshots store only what changed: five of
   them took 60 MB, well inside Fly's free 10 GB. They're encrypted like the disk and reachable only
   through the Fly account, so they're as private as the host.

   **Off the host, shown rather than assumed.** Fly's docs don't say where snapshots are kept, so
   the restore test put its copy in a different zone, which means a different server, and every file
   came back.

   **What it doesn't cover:**
   - **Up to a day of changes.** Before anything risky, take a snapshot by hand:
     `fly volumes snapshots create <volume id>`.
   - **Losing the Fly account.** If billing lapses, the account is locked or the app is deleted, the
     snapshots go with it, and Fly itself says they "shouldn't be your primary backup method". A
     nightly encrypted copy to Cloudflare R2 would close that gap. On 2026-10-15 it was weighed and
     left for later: the data is 80 KB, and the likely failures are a dead server or quiet damage,
     which snapshots cover.
   - **Deleted addresses linger.** Unsubscribing deletes the record ([PRD 15](15-email-subscriptions.md)),
     but it stays in snapshots for up to 30 days, and Fly can't delete a snapshot early. A restore
     also brings back anyone who unsubscribed after that snapshot, and they would get the next
     email. With daily snapshots that's at most a day's worth; the metrics totals show how many.

   **Runbook: checking a backup.** This is the drill run on 2026-10-15. It touches nothing live.
   1. Find the disk, then its snapshots:
      `fly volumes list -a stumpty`, then `fly volumes snapshots list <volume id>`.
   2. Restore a snapshot into a new disk, in a different zone from the live one:
      ```bash
      fly volumes create restore_test --snapshot-id <snapshot id> --size 1 --region ord \
        --require-unique-zone --unique-zone-app-wide -a stumpty
      ```
   3. Mount it on a throwaway machine. Run this from a folder outside the repo, so nothing in
      fly.toml can reach the machine. The machine runs only `sleep` and stops itself after 30
      minutes. It has no ports, so it gets no traffic, and it runs none of the site's code, so it
      sends no email:
      ```bash
      fly machine run python:3.13-alpine sleep 1800 -a stumpty --region ord \
        --volume <new volume id>:/restore --name restore-test --vm-memory 256 --restart no
      ```
   4. From the repo folder, run `deploy/check-restore.py` on it. The script prints sizes, counts,
      dates and short hashes. It never prints an address or the key, and it ends `RESULT: OK` or
      `FAIL`:
      ```bash
      fly ssh console -a stumpty --machine <restore machine id> -C "sh -c 'echo $(base64 < deploy/check-restore.py | tr -d '\n') | base64 -d > /tmp/check.py && python3 -I /tmp/check.py /restore'"
      ```
   5. Compare with the live machine. Any file not written since the snapshot must have the same
      size and hash:
      ```bash
      fly ssh console -a stumpty --machine <live machine id> -C "sh -c 'cd /app/App_Data && ls -l --time-style=+%FT%RZ *.json keys && sha256sum teasers.json issues.json suggestions.json weekly-summary.json keys/* | cut -c1-12,65-'"
      ```
   6. Remove both. **Check each ID first:** the live machine runs the `stumpty:` image, and the
      live disk is `stumpty_data`.
      `fly machine destroy <restore machine id> --force -a stumpty`, then
      `fly volumes destroy <new volume id> -a stumpty`.

   **Runbook: recovering for real** (not yet rehearsed; the drill proves the data, not this swap).
   Take a snapshot of the damaged disk first, if it still exists. Then restore the chosen snapshot
   as in step 2, naming it `stumpty_data` instead of `restore_test`, because fly.toml mounts the disk
   by name. Destroy the damaged disk and the live machine, so a deploy can't pick the old disk back
   up, then run `fly deploy --ha=false`, which makes a new machine on the restored disk. Write down
   the old disk's ID before destroying it. According to Fly's docs, its snapshots outlive it until
   their retention runs out, but they can only be found by that ID.
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
    | `Site__BaseUrl` = `https://stumpty.com` | Yes | **Fails silently** — every link in every email is built from it, and so is the link preview's picture; it defaults to a local address ([PRD 15](15-email-subscriptions.md), [PRD 09](09-visual-design.md#the-link-preview)) |

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

    **The first upload is built (2026-09-25).** On a new, empty disk, `deploy/start.sh` waits for
    the bank instead of letting the site crash and restart in a loop, and
    `fly ssh sftp put OneADay/App_Data/teasers.json /app/App_Data/teasers.json` delivers it. The
    one command for later updates is still to build.

    **Pictures go up with the bank** ([PRD 05](05-authoring-and-admin.md)), one file each, into
    `/app/App_Data/teaser-images/`. One left behind shows as a broken image on the page and in the
    email, so the site checks at every start, which is when it reads a new bank, and logs a
    warning for each picture it lacks, with the command that uploads it *(built 2026-10-18;
    checked by hiding a picture and restarting)*.

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
    - **Health checks:** fly.toml defines none. An HTTP check would get the lock's 403, and Fly
      restarts the machine if the app exits.
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

15. **Compress pages.** ✅ **Built 2026-09-25.** Cloudflare compresses what it hands visitors, but Fly
    bills what leaves the server, before Cloudflare. Measured on the published build, gzip at its
    best setting makes every page about 2.4 times smaller (the home page goes from 11.6 KB to 5.3 KB,
    headers included) for under 15% more CPU (`Services/PageCompression.cs`). The most a flood of
    page requests could cost drops by the same 2.4 times, and one rate-limited attacker from about
    $1.51 a month to $0.70. Brotli came out within 1%, so gzip alone. About 1.8 KB of every page is
    encrypted Blazor data that can't compress, which is why it isn't more.

    Compression is switched on for HTTPS deliberately: behind Cloudflare every request is HTTPS, and
    compression skips HTTPS by default. The default exists because compression can leak a secret
    (the BREACH attack) when a page shows back text a visitor controls next to that secret. The page
    shrinks a little whenever the visitor's text matches part of the secret, so an eavesdropper
    watching response sizes can learn it a character at a time. **No Stumpty page shows back its
    address.** Only Unsubscribe and ConfirmSubscription read it, and both use the token to find a
    subscriber without displaying it. A future page that echoes its address next to anything
    secret must be left out of compression.

16. **Ten live connections per address.** ✅ **Built 2026-09-28**, after the security review. Every
    open page keeps a live connection, and fly.toml caps the machine at 300 at once so they can't
    run it out of memory. Cloudflare's rate limit only slows how fast connections are opened, not
    how many stay open, so one script could hold all 300 in about five minutes and lock every real
    visitor out. Now one address may hold ten (`Services/LiveConnectionCap.cs`); an eleventh gets an
    empty 429, while pages keep loading. Filling the machine takes thirty addresses.

    - **An IPv6 household counts as one address.** A home gets a whole /64 block and its devices
      pick addresses from it at will, so the cap counts the block.
    - **After the proxy headers**, or every visitor would share Cloudflare's address, and one cap.
    - **Ten is ten tabs across a household.** A school or office behind one address could need more;
      raise `LiveConnections__MaxPerAddress` if one ever does. Zero switches the cap off.

    The same review also put server-side limits on what visitors can store — field lengths, and
    ceilings on reports, suggestions and unconfirmed sign-ups ([PRD 06](06-community-feedback.md),
    [PRD 15](15-email-subscriptions.md)) — so that nothing sent over a live connection can grow a
    file, or the memory holding it, without end.

17. **Run the tests on every change.** ✅ **Built 2026-10-15.** Until then the suite ran only on the
    author's Mac, so it protected nothing unless someone remembered to run it.
    `.github/workflows/tests.yml` now runs `dotnet test OneADay.Tests`, the README's own command, on
    GitHub's Linux machines. It runs for every pull request into master and every push to master,
    and a pull request shows a red ✗ when any test fails.

    - **Linux, like the server.** Before it was switched on, the suite ran in Microsoft's .NET 10
      Linux image on x64 and arm64, as an ordinary user rather than root. All 706 tests passed,
      eight runs in a row, and none needed changing. Three test classes lean on the operating
      system: `DeployFilesTests` runs `deploy/pull-live-data.sh` under Linux's `sh` with a
      stand-in `fly`, `SubscriptionTests` asks `git` itself, and `AtomicFileTests` watches for the
      temporary file. Their code was broken on purpose there, and each test failed, so they check
      the same things on Linux as on the Mac.
    - **A clean checkout.** CI sees only what is committed: no `App_Data/`, no question bank and no
      secrets. Every test builds its own throwaway data, and none may start to need the real files.
    - **Least access.** The workflow's token can only read the repo and isn't left on disk for the
      tests to find. Pull requests from other people's copies of the repo get no secrets at all.
      Each action is pinned to an exact commit, because a version tag can later be moved to other
      code.
    - **The newest .NET 10 SDK** (`10.0.x`), like the Dockerfile's `sdk:10.0`, so CI builds with
      what Fly's builder uses.
    - **Binding only once required.** On its own, a red run warns but doesn't stop a merge. To make
      it binding, require the `test` check for master in the repo's settings, under branch
      protection or rulesets.
    - **Deploying on merge is not built.** Deploys stay manual (`fly deploy --ha=false`, see the
      README). Automating them would need an app-scoped Fly deploy token, stored as the repository
      secret `FLY_API_TOKEN`. The author sets that secret in GitHub's settings; it never goes in a
      file.

## Candidate hosts

| Host | Fit | Cost |
|---|---|---|
| **Fly.io** | Best fit — lasting storage, long connections, always on | ~$4/month (512MB + 1GB volume) |
| **Azure App Service** | First-class .NET, least friction | ~$13/month (B1) |
| **A small rented server (VPS)** | Most control, most upkeep — and the only option where the proxy is on the same machine, so it can be named exactly | ~$5–6/month |
| **Raspberry Pi at home** | Cheapest; needs a tunnel, and home uptime caveats | hardware |

On any platform host, **switch off sleeping when idle** ("scale to zero") — see Risks.

**Chosen on 2026-09-25: Fly.io**, the cheapest host that meets every requirement above without
running a server yourself. Measured for this app: 512 MB is plenty, and the bill is about $4.20 a
month. The deploy files are `Dockerfile`, `fly.toml` and `deploy/start.sh`; the steps are in the
README. Fly has no free tier: its trial allows 2 hours of machine time and stops machines after
5 minutes, so it can't host the site.

`.dockerignore` decides what `fly deploy` uploads to Fly's builder: everything not listed goes.
It leaves out `App_Data/` and `OneADay/BrainTeaserQuestions.txt`, the early question draft with its
answers. The draft was missed at first and caught by the security review of 2026-09-28;
`DeployFilesTests` now checks both.

> **History — the first deploy had dead buttons** (2026-09-30). The `Dockerfile` restored packages
> from the `.csproj` alone, a common Docker habit. The SDK only fetches Blazor's own script when it
> can see `.razor` files at restore time, so the build left out `blazor.web.js`: pages showed, but
> nothing on them responded. The build now copies the whole project first, and `DeployFilesTests`
> fails if any `dotnet` step runs before that copy.

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
- [x] A backup has been taken **and restored** once. **2026-10-15:** the daily 04:51 UTC snapshot
      was restored into a new disk in another zone (e543; the live disk is in acb2) and mounted on a
      throwaway machine.
      - All 9 files were there and parsed: 25 teasers, rotation history through Oct 14 (the day the
        snapshot was taken), the subscriber list (counted, never read) and the Data Protection key.
      - The five files not written since the snapshot matched the live copies byte for byte:
        teasers, issues, suggestions, weekly-summary and the key. The other four had changed since,
        as they should have.
      - The machine and disk were then removed.
- [x] `DeployFilesTests` pins 30 days of snapshots in fly.toml's `[[mounts]]`. Mutation-verified
      with three breaks, each caught: 5 days, the line removed, and the line moved to another
      section.
- [ ] Links in a real email open the public site
- [x] Deploy command documented in the README
- [ ] The server's own `.fly.dev` address gets an empty 403, while stumpty.com loads

### The Cloudflare-only lock — `CloudflareLockTests`

- [x] Without the header, or with anything but the exact secret (a prefix, extra characters, other
      letter case, a second value alongside it), a request gets an empty 403; the exact secret gets
      the page
- [x] A second copy of the header on its own line is refused, even with the secret first — the sign
      of a rule that adds instead of setting
- [x] With no settings at all the lock is on and refuses to start; a missing or short secret refuses,
      and so do forty spaces
- [x] Proven against the real files: the lock is the first thing in Program.cs's pipeline, and
      appsettings.json switches it on while holding no secret
- [x] Mutation-verified: six breaks, each caught — letting a missing header through, accepting any
      value, moving the lock below the static files, defaulting it off, accepting a short secret, and
      switching it off in the production settings

### The connection cap — `LiveConnectionCapTests`, over real requests on the local machine

- [x] One address holds its cap and the next connection gets a 429; another address is unaffected;
      closing a connection makes room again
- [x] An IPv6 household shares one cap, and an IPv4 visitor written as IPv6 is the same visitor
- [x] Pages are never counted or turned away, including a path that merely starts with `/_blazor`
- [x] Switched off, nothing is turned away
- [x] Proven against the real files: the cap comes after the proxy headers and before the pages,
      and appsettings.json sets ten

### Page compression — `PageCompressionTests`

- [x] A page goes out gzip-compressed to anyone who accepts it and arrives identical; a client that
      can't take gzip — one asking only for Brotli, which isn't offered — gets the plain page
- [x] Pages are compressed over HTTPS, as every request is behind Cloudflare
- [x] gzip runs at its best setting, which the measured savings assume
- [x] Proven against the real Program.cs: compression is registered, and comes before everything
      that writes a page
- [x] Mutation-verified: five breaks, each caught by the test written for it
- [x] Checked in the running app on 2026-09-25: the home page went out as 4,462 of its 10,016 bytes,
      and the live connection still worked

### Continuous integration — `.github/workflows/tests.yml`

- [x] The whole suite passes on Linux in a clean checkout, as an ordinary user, on x64 and arm64,
      eight runs in a row
- [x] Mutation-verified on Linux: four breaks, each caught, and each makes `dotnet test` exit with 1,
      which is what fails the run. The breaks were: the download script fetching
      `subscribers.json`, the script skipping its running-site check, `.gitignore` letting
      `App_Data/` back in, and the save's temporary file moving to the system temp folder
- [ ] A pull request shows a green run
- [ ] A deliberately failing test turns a pull request red

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

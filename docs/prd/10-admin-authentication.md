# PRD 10 — Admin access

**Status:** Spec of record · **Code:** `Services/AdminAccess.cs`, `Components/Routes.razor`

## In plain terms

The admin page adds, edits and deletes puzzles, shows every answer before its day, and clears the
suggestion and report queues. It used to have no protection at all — only the fact that it ran on the
author's Mac, which stops being true the moment the site goes online — and it was linked from the
public menu. There are no user accounts, and none are wanted ([PRD 00](00-product-overview.md)).

So rather than put a password on it, **the live site simply doesn't have one.** The page exists only
when the app runs on the author's Mac; everywhere else `/admin` is "not found". A page that was
never built has no buttons to press.

## Decision

**No admin page on the live site** *(author's decision, 2026-09-15)*. A passphrase was the earlier
recommendation, rejected because a lock can have a flaw — a guessable phrase, one action that
forgets to check it — and nobody watches a one-person site for someone working at it.

| Option | Verdict |
|---|---|
| Passphrase on `/admin` | Rejected — a lock to get right, and to keep right |
| Sign-in run by the host (proxy, Easy Auth, Cloudflare Access) | Rejected — safety would hang on one host's settings |
| Accounts, or signing in with another service (OAuth) | Rejected — the machinery PRD 00 removed on purpose |
| Delete the page; edit the data by hand | Rejected — loses the date, duplicate and image safeguards ([PRD 05](05-authoring-and-admin.md)) |
| Admin commands to call by hand | Rejected — the same door with a worse handle |
| **Admin only on the author's machine** | **Chosen** |

## How it works

The app knows where it's running: **Development** on the author's Mac, **Production** on a server.
Admin exists only in Development (`AdminAccess.IsAvailable`), so it **fails closed** — Production,
Staging or a misspelt setting all mean no admin page. A page can be reached two ways, so there are
two checks:

1. **Asking the server for `/admin`** — typing it, a bookmark, a script. A check early on answers "not
   found" before any page code runs. The not-found page greets an admin knock with a cat and *"Nice
   try, nothing to see here"*; a mistyped address gets a plain apology.
2. **Clicking through inside a page already open.** That never reaches the server, so the first check
   can't see it; the page router checks too, and shows not-found instead of building the page.

Either check alone leaves the other door open. Since the page is never built on the live site, none
of its buttons exist there. **The menu never links to `/admin`**, in any mode — a link that appears
only sometimes is one more thing to get wrong. The author uses a bookmark.

## Development mode is fenced in

All of this rests on the live site never running in Development, which switches admin on for anyone
who can reach it. So the app **refuses to start** in Development unless both are true — checked
before anything else, so a refused start never opens the site (`Services/DevelopmentModeGuard.cs`):

1. **It's a Debug build** — the development build, never the Release build that gets deployed. This
   stops the commonest accident: a server set to Development by a line copied from a tutorial
   (`ASPNETCORE_ENVIRONMENT=Development`).
2. **The machine is marked as the author's**, by a flag in private local settings (user-secrets, kept
   outside the project). This stops the sneakier one: a server that copies the code and runs
   `dotnet run`, which switches Development on by itself. A fresh copy has no such flag.

Each check alone leaves one accident open. Checking where visitors connect from was rejected: behind
a proxy or tunnel, everyone appears to come from the machine itself. Refusal messages explain how to
fix a server but deliberately omit the command that marks a machine, since whoever reads one may be
standing on the server.

**One-time setup on the author's computer:**

```bash
dotnet user-secrets set "AuthorsMachine" "true" --project OneADay
```

> **Never run this on a server.** A published Release build refuses Development regardless, but a
> server running `dotnet run` would not.

**What this can't stop:** someone who deliberately marks a server *and* runs a Debug build there —
two deliberate steps past a warning, beyond what an app can defend against.

## Who writes what

The design stays simple because **every file has exactly one writer**:

| Written by | Files |
|---|---|
| The author's Mac, through `/admin` | `teasers.json`, `teaser-images/` |
| The live site, through visitors | `stats.json`, `rotation.json`, `suggestions.json`, `issues.json`, `subscribers.json` |

Nothing on the live site changes a teaser, so publishing is copying, never merging. **To publish:**
add teasers in `/admin`, copy **only** `teasers.json` and any new images into the server's
`App_Data/`, and restart. The bank is read once, at startup.

> **Never copy the whole `App_Data/` folder up** — it would overwrite live statistics, rotation
> history and real subscribers with the Mac's test copies. That rule belongs in the publish command,
> so nobody has to remember it ([PRD 11](11-deployment.md)).

**The build never ships data.** `dotnet publish`, which packages the app for a server, included every
`.json` file by default — so it was copying all of `App_Data/`, every future answer and subscriber
address, into the package. `OneADay.csproj` now leaves it out. *(Found 2026-09-15, while testing
this.)*

**A bad bank stops the app.** A missing bank used to be replaced by three sample riddles; the live
site now refuses to start and names the missing file, while the author's machine still starts with
samples so a fresh copy runs. An unreadable bank used to fail on the first visitor's request; now it
stops the app everywhere and is **left untouched**. Loading at startup means a bad publish fails the
deploy, not a visitor's page.

## What the author gives up

- **Admin actions on live data only change the Mac's copy** — deleting a suggestion or report,
  setting a status, resetting the rotation box. Suggestions and reports still arrive by email
  ([PRD 14](14-email-notifications.md)).
- **The rotation panel and statistics show the Mac's data** unless the live files are copied down.
  Copy everything **except `subscribers.json`**: the Mac holds the real Gmail password, so a copy of
  the real list could send the daily email to real subscribers.

## Non-goals

- Any way to administer the live site from a browser
- Several authors, roles, or an audit trail

## Acceptance criteria

### The server check — `AdminGateTests`, real requests on the local machine

- [x] Outside Development, `/admin`, `/admin/`, `/ADMIN` and `/admin/anything` are all 404 and never
      reach the app — Staging as well as Production (mutation-verified)
- [x] The rest of the site is untouched, `/administrator` included; in Development `/admin` is
      reachable — the control case

### The router and the menu — `AdminRoutingTests`

- [x] On the live site, in-app navigation to `/admin` shows not-found and never builds the page — its
      services aren't registered in the test, so building it would fail (mutation-verified)
- [x] In Development the page builds, answers included — the control case; the menu never links to
      `/admin` in either mode

### The bank at startup — `TeaserStoreStartupTests`

- [x] A missing bank stops the live site, and nothing is written in its place (mutation-verified); in
      Development it still starts with samples
- [x] An unreadable bank stops the app everywhere and is left exactly as it was

### The Development-mode fence — `DevelopmentModeGuardTests`

- [x] Production and Staging are never refused, whatever the build or flag
- [x] A Release build refuses Development even on a marked machine (mutation-verified)
- [x] An unmarked machine refuses Development — flag missing, empty, false or garbage
      (mutation-verified); the author's marked computer still starts — the control case
- [x] No refusal message contains the command that would silence it (mutation-verified — that run
      caught a hole in the test itself: with the guard off there was no message at all, so the check
      passed trivially; it now asserts a refusal happened first)

### By hand

- [x] Unmarked machine in Development refuses before any port opens; starts once marked
- [x] A published Release copy refuses Development **even with the flag present**, and starts normally
      in Production — `/about` loads, `/admin` is 404
- [x] A published build contains no `App_Data/`; with no bank it refuses to start and names the
      missing file
- [x] In-app navigation to `/admin` on the published copy shows not-found without the browser asking
      the server — so the router's check is what stopped it; neither menu links to `/admin`

### Not covered

- [ ] The publish command — waits on the choice of host ([PRD 11](11-deployment.md))
- [ ] Copying live data down to the Mac — not scripted

## Implementation notes

`Services/DevelopmentModeGuard.cs` is the fence; `Services/AdminAccess.cs` holds the rule and the
server check; `Components/Routes.razor` the router check; `Components/Layout/MainLayout.razor` has no
admin link; `Services/TeaserStore.cs` refuses a missing or unreadable bank; `OneADay.csproj` keeps
`App_Data/` out of build output. `Program.cs` runs the fence first, loads the bank at startup, and
places the server check after the not-found handling. In `.claude/launch.json`,
`oneaday-published` runs a published copy in Production on port 5179, and
`guard-check-published-in-development` runs it in Development, where it must refuse to start.

> **History — found while building:** the router first showed not-found with `<Pages.NotFound />`.
> Razor can't read a dotted tag name as a component, so it printed an empty element — an in-app visit
> to `/admin` would have been a blank page. The routing test caught it; `DynamicComponent` fixed it.

# PRD 10 — Admin access

**Status:** Spec of record · **Code:** `Services/AdminAccess.cs`, `Components/Routes.razor`

## In plain terms

The admin page can add and delete puzzles and shows every future answer. Rather than put a
password on it, **the live site simply doesn't have one** — the page only exists when the app
runs on the author's Mac. A page that was never built has no buttons for anyone to press.

## Problem

`/admin` adds, edits and deletes teasers, shows every answer before its day, and clears the
suggestion and issue queues. It had no protection at all — the only thing keeping it private
was running on the author's Mac, which stops being true the moment the site is deployed. It
was also linked from the public menu. There are no user accounts and none are wanted
([PRD 00](00-product-overview.md)).

## Decision

**The live site has no admin page.** It exists only on the author's machine; everywhere else
`/admin` is not found. *(Author's decision, 2026-09-15.)*

A passphrase was the earlier recommendation and was rejected: a lock can have a flaw — a
guessable phrase, one action that forgets to check — and nobody watches a one-person site for
someone working at it.

| Option | Verdict |
|---|---|
| Passphrase on `/admin` | Rejected — a lock to get right, and to keep right |
| Host-level auth (proxy, Easy Auth, Cloudflare Access) | Rejected — safety would hang on one host's settings |
| Accounts or OAuth | Rejected — the machinery PRD 00 removed on purpose |
| Delete the page; edit JSON by hand | Rejected — loses the date, duplicate and image safeguards ([PRD 05](05-authoring-and-admin.md)) |
| Admin APIs to call by hand | Rejected — the same door with a worse handle |
| **Admin only on the author's machine** | **Chosen** |

## How it works

The app knows where it's running: **Development** on the author's Mac, **Production** on a
host. `AdminAccess.IsAvailable` is simply "is this Development?", so it **fails closed** —
Production, Staging, or a misspelt environment name all mean no admin page.

There are two ways to reach a page, so there are two checks:

1. **Asking the server for `/admin`** — typing it, a bookmark, a script. A gate early in the
   request pipeline answers with 404 before any page code runs. The not-found page greets an
   admin knock with a cat and "Nice try, nothing to see here"; a mistyped address still gets a
   plain apology.
2. **Clicking through inside a page that's already open.** That navigation happens inside the
   live connection and never reaches the server, so the gate can't see it. The router checks
   too, and renders not-found instead of building the page.

Either check alone leaves the other door open. And because the page is never built on the
live site, none of its buttons exist there.

**The menu never links to `/admin`**, in any mode — a link that appears only sometimes is one
more thing to get wrong. The author uses a bookmark, on the one machine where it exists.

## Development mode is fenced in

All of the above rests on the live site never running in Development, because that mode turns
the admin page on for anyone who can reach it. So the app **refuses to start** in Development
unless both are true, checked before anything is built so a refused start never opens a port
(`Services/DevelopmentModeGuard.cs`):

1. **It's a Debug build.** Anything deployed is a Release build. This stops the commonest
   accident: a host with `ASPNETCORE_ENVIRONMENT=Development` copied from a tutorial.
2. **The machine is marked as the author's**, by a flag in user-secrets. This stops the
   sneakier one: a server that clones the repo and runs `dotnet run`, which switches
   Development on by itself. A fresh clone has no user-secrets, so no flag.

Each check alone leaves one accident open. Checking where visitors connect from was rejected
— behind a proxy or tunnel, everyone arrives from localhost. Refusal messages explain how to
fix a server but deliberately omit the command that marks a machine, since whoever reads one
may be standing on the server.

**One-time setup on the author's computer:**

```bash
dotnet user-secrets set "AuthorsMachine" "true" --project OneADay
```

> **Never run this on a server.** A published Release build refuses Development regardless,
> but a server running `dotnet run` would not.

**What this can't stop:** someone who deliberately marks a server *and* runs a Debug build
there. That's two deliberate steps against a warning, past what an app can defend against.

## Who writes what

The design stays simple because **every file has exactly one writer**:

| Written by | Files |
|---|---|
| The author's Mac, through `/admin` | `teasers.json`, `teaser-images/` |
| The live site, through visitors | `stats.json`, `rotation.json`, `suggestions.json`, `issues.json`, `subscribers.json` |

Nothing on the live site ever changes a teaser, so publishing is copying — never merging.

**To publish:** add teasers in `/admin`, copy **only** `teasers.json` and any new images into
the server's `App_Data/`, restart. The bank is read once, at startup.

> **Never copy the whole `App_Data/` folder up** — it would overwrite live stats, rotation
> history and real subscribers with the Mac's test copies. That rule belongs in the publish
> command so nobody has to remember it ([PRD 11](11-deployment.md)).

**The build never ships data.** `dotnet publish` includes every `.json` file by default, so it
was copying all of `App_Data/` — every future answer and every subscriber address — into its
output. `OneADay.csproj` now excludes it. *(Found 2026-09-15, while testing this.)*

**A bad bank stops the app.** A missing bank used to create three sample riddles in its place;
the live site now refuses to start and names the file, while the author's machine still starts
with samples so a fresh checkout runs. An unreadable bank used to fail on the first visitor's
request; it now refuses to start in every environment and **leaves the file untouched**. The
bank loads at startup, so a bad publish fails the deploy rather than a visitor's page.

## What the author gives up

- **Admin actions on live data only change the Mac's copy** — deleting a suggestion or report,
  setting a status, resetting the rotation box. Suggestions and reports still reach the inbox
  ([PRD 14](14-email-notifications.md)).
- **The rotation panel and stats show the Mac's data**, unless the live files are copied down.
  Copy everything **except `subscribers.json`**: the Mac holds the real Gmail password, so a
  copy of the real list could send the daily email to real subscribers.

## Non-goals

- Any way to administer the live site from a browser
- Multiple authors, roles, or an audit trail

## Acceptance criteria

### The gate — `AdminGateTests`, real requests on a loopback port

- [x] Outside Development, `/admin`, `/admin/`, `/ADMIN` and `/admin/anything` are all 404 and
      never reach the app — Staging as well as Production (mutation-verified)
- [x] The rest of the site is untouched, `/administrator` included
- [x] In Development `/admin` is reachable — the control case

### The router and the menu — `AdminRoutingTests`

- [x] On the live site, in-app navigation to `/admin` shows not-found and never builds the
      page — its services aren't registered in the test, so building it would fail
      (mutation-verified)
- [x] In Development the page builds, answers included — the control case
- [x] The menu never links to `/admin`, in either mode

### The bank at startup — `TeaserStoreStartupTests`

- [x] A missing bank stops the live site, and nothing is written in its place (mutation-verified)
- [x] A missing bank in Development still starts with samples
- [x] An unreadable bank stops the app everywhere and is left exactly as it was

### The Development-mode fence — `DevelopmentModeGuardTests`

- [x] Production and Staging are never refused, whatever the build or flag
- [x] A Release build refuses Development even on a marked machine (mutation-verified)
- [x] An unmarked machine refuses Development — flag missing, empty, false or garbage
      (mutation-verified)
- [x] The author's marked computer still starts — the control case
- [x] No refusal message contains the command that would silence it (mutation-verified — and
      that run caught the test's own hole: with the guard off there was no message at all, so
      the check passed trivially. It now asserts a refusal happened first)

### By hand

- [x] Unmarked machine in Development refuses before any port opens; starts once marked
- [x] A published Release copy refuses Development **even with the flag present**
- [x] …and starts normally in Production — `/about` loads, `/admin` is 404
- [x] A published build contains no `App_Data/`
- [x] That published app with no bank refuses to start and names the missing file
- [x] In-app navigation to `/admin` on the published copy shows not-found, and the browser
      never asks the server for it — so the router's check is what stopped it
- [x] Neither menu links to `/admin`

### Not covered

- [ ] The publish command — waits on the choice of host ([PRD 11](11-deployment.md))
- [ ] Copying live data down to the Mac — not scripted

## Implementation notes

| File | Role |
|---|---|
| `Services/DevelopmentModeGuard.cs` | Refuses Development unless Debug build on a marked machine |
| `Services/AdminAccess.cs` | The rule, and the request gate |
| `Components/Routes.razor` | The router check, for in-app navigation |
| `Components/Layout/MainLayout.razor` | No admin link, in any mode |
| `Services/TeaserStore.cs` | Refuses to start on a missing or unreadable bank |
| `Program.cs` | Fence first; bank at startup; gate after the not-found re-execution |
| `OneADay.csproj` | Keeps `App_Data/` out of build and publish output |
| `.claude/launch.json` | `oneaday-published` runs a published copy in Production on 5179; `guard-check-published-in-development` runs the same copy in Development and must refuse to start |

> **Found while building.** The router first rendered `<Pages.NotFound />`. Razor can't read a
> dotted tag name as a component, so it printed an empty element — an in-app visit to `/admin`
> would have shown a blank page. The routing test caught it; `DynamicComponent` fixed it.

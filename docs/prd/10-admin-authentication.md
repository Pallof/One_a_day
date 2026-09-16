# PRD 10 — Admin access

**Status:** Spec of record · **Code:** `Services/AdminAccess.cs`, `Components/Routes.razor`

## Problem

`/admin` adds, edits and deletes teasers, shows every answer before its day — scheduled
ones included — and clears the suggestion and issue queues. It had no protection at all.
The only thing keeping it private was that the app ran on the author's Mac, which stops
being true the moment the site is deployed. It was also linked from the public menu.

There are no user accounts and none are wanted ([PRD 00](00-product-overview.md)).

## Decision

**The live site has no admin page.** It exists only on the author's machine; everywhere
else, `/admin` is not found. *(Author's decision, 2026-09-15.)*

The earlier recommendation was a passphrase. It was rejected because every exposed admin
surface is a standing liability, and this project isn't going to spend the time building
and maintaining a strong lock. A lock can have a flaw — a guessable passphrase, one admin
action that forgets to check it — and nobody watches a one-person site for someone working
at it. **A page that is never built has no buttons for a script to press.**

| Option | Verdict |
|---|---|
| Passphrase on `/admin` | Rejected — a lock to get right, and to keep right |
| Host-level auth (proxy, Easy Auth, Cloudflare Access) | Rejected — the app's safety would hang on one host's settings |
| Accounts or OAuth | Rejected — the machinery PRD 00 removed on purpose |
| Delete the page; edit JSON by hand | Rejected — gives up the date, duplicate and image safeguards that keep weekly authoring cheap ([PRD 05](05-authoring-and-admin.md)) |
| Admin APIs to call by hand | Rejected — an API that writes teasers is the same door with a worse handle |
| **Admin only on the author's machine** | **Chosen** |

## How it works

The app always knows where it's running. On the author's Mac it runs in **Development**
(set by `launchSettings.json`); a host runs it in **Production** unless told otherwise.
`AdminAccess.IsAvailable` is simply "is this Development?", so it **fails closed** —
Production, Staging, or a misspelt environment name all mean no admin page.

There are two ways to reach a page, so there are two checks:

1. **Asking the server for `/admin`** — typing it, a bookmark, a script. A gate early in
   the request pipeline answers anything under `/admin` with 404 before any page code runs,
   and the not-found page that follows answers an admin knock with a cat and "Nice try,
   nothing to see here" — a mistyped address still gets a plain apology.
2. **Clicking through inside a page that's already open.** Blazor handles that navigation
   inside the live connection, so no request for `/admin` ever reaches the server and the
   gate can't see it. The router checks as well, and renders the not-found page instead of
   building the admin page.

Either check alone leaves the other door open. And since the page is never built on the
live site, none of its buttons exist there: Blazor only accepts events for components it
actually rendered into a visitor's session.

**The menu never links to `/admin`**, in any mode. It is the visitor's map of the site, and a
link that appears only in some conditions is one more thing to get wrong; the author opens the
page from a bookmark, on the one machine where it exists.

## Development mode is fenced in

Everything above rests on the live site never running in Development mode, because in that
mode the admin page is on — for anyone who can reach the site. So the app **refuses to
start** in Development unless two things are true. Both are checked before anything is
built, so a refused start never opens a port (`Services/DevelopmentModeGuard.cs`):

1. **It's a Debug build.** Anything published and deployed is a Release build. This stops
   the commonest accident: a host with `ASPNETCORE_ENVIRONMENT=Development` set, often
   copied from a tutorial to see detailed error pages.
2. **The machine is marked as the author's computer**, by a flag in user-secrets. This
   stops the sneakier one: a server that clones the repo and uses `dotnet run`, which
   switches Development on by itself from `launchSettings.json`. A fresh clone has no
   user-secrets, so it has no flag.

Each check alone leaves one of those accidents open. Checking where visitors connect from
was considered and rejected: behind a reverse proxy or a tunnel, every visitor arrives from
localhost.

The refusal messages explain how to fix a server, and deliberately don't print the command
that marks a machine — whoever reads one may be standing on the server.

### One-time setup on the author's computer

```bash
dotnet user-secrets set "AuthorsMachine" "true" --project OneADay
```

> **Never run this on a server.** It's the one thing that tells the app "this is the
> author's computer, let the admin page on". A published Release build still refuses
> Development regardless, but a server running `dotnet run` would not.

**What this can't stop:** someone who deliberately marks a server *and* runs a Debug build
there. That takes two deliberate steps against a warning that says not to, which is past
what an app can defend against. Access to the server itself is a matter for the hosting
account's security, not this code.

## Who writes what

The design stays simple because **every file has exactly one writer**:

| Written by | Files |
|---|---|
| The author's Mac, through `/admin` | `teasers.json`, `teaser-images/` |
| The live site, through visitors | `stats.json`, `rotation.json`, `suggestions.json`, `issues.json`, `subscribers.json` |

Nothing on the live site ever changes a teaser, so publishing is copying — never merging.

### Publishing

1. Add teasers in `/admin` on the Mac, with every safeguard in PRD 05.
2. Copy **only** `teasers.json` and any new images into the server's `App_Data/`.
3. Restart the app. The bank is read once, at startup.

> **Never copy the whole `App_Data/` folder up.** It would overwrite live stats, rotation
> history and real subscribers with the Mac's test copies. The publish command is where
> that rule should live, so nobody has to remember it. It depends on the host, so it
> belongs to [PRD 11](11-deployment.md).

**The build never ships data.** `dotnet publish` includes every `.json` file in the
project by default, so it copied all of `App_Data/` into its output — every future answer
and every real subscriber address. Deploying that output would have been the whole-folder
copy by accident, repeated on every deploy. `OneADay.csproj` now marks `App_Data/` as never
published and never copied to build output. *(Found 2026-09-15, while testing this.)*

### A bad bank stops the app

Publishing makes a missing or broken `teasers.json` a realistic failure, and the old
behaviour hid both:

- **Missing.** The app used to create three sample riddles and save them in the bank's
  place. The live site now **refuses to start** and names the file. The author's machine
  still starts with samples, so a fresh checkout just runs.
- **Unreadable.** The app used to fail on the first visitor's request. It now refuses to
  start, in every environment, and **leaves the file untouched** so it can be fixed and
  published again.

The bank loads at startup rather than on first use, so a bad publish fails the deploy
instead of a visitor's page.

## What the author gives up

- **Admin actions on live data only change the Mac's copy** — deleting a suggestion or an
  issue report, setting a report's status, and resetting the rotation box. Suggestions and
  reports still reach the author's inbox ([PRD 14](14-email-notifications.md)).
- **The rotation panel and stats show the Mac's data**, not the live site's, unless the live
  files are copied down first. Copy everything **except `subscribers.json`**: the Mac holds
  the real Gmail app password in Development, so a copy of the real list there could send
  the daily email to real subscribers.

## Non-goals

- Any way to administer the live site from a browser
- Multiple authors, roles, or an audit trail

## Acceptance criteria

### The gate — `AdminGateTests`, real requests on a loopback port

- [x] Outside Development, `/admin`, `/admin/`, `/ADMIN` and `/admin/anything` are 404 and
      never reach the app — in Staging as well as Production (mutation-verified: a rule
      that always allows admin fails all five)
- [x] The rest of the live site is untouched, `/administrator` included
- [x] In Development, `/admin` is reachable — the control case

### The router and the menu — `AdminRoutingTests`

- [x] On the live site, navigating to `/admin` inside the app shows the not-found page and
      never builds the admin page — its services aren't even registered in the test, so
      building it would fail (mutation-verified: switching the router check off fails it)
- [x] In Development the admin page builds, answers included — the control case, and the
      reason any of this matters
- [x] The menu never links to `/admin`, in Development or Production

### The bank at startup — `TeaserStoreStartupTests`

- [x] A missing bank stops the live site, and nothing is written in its place
      (mutation-verified: letting the live site create samples fails both cases)
- [x] A missing bank in Development still starts with samples
- [x] An unreadable bank stops the app in every environment and is left exactly as it was

### The Development-mode fence — `DevelopmentModeGuardTests`

- [x] Production and Staging are never refused, whatever the build or flag
- [x] A Release build refuses Development, even on a marked machine (mutation-verified)
- [x] An unmarked machine refuses Development — flag missing, empty, false or garbage
      (mutation-verified: removing the check fails all four)
- [x] The author's marked computer still starts in Development — the control case
- [x] No refusal message contains the command or the flag that would silence it
      (mutation-verified — and the run caught the test's own hole: with the guard switched
      off there was no message at all, so "doesn't contain the command" passed trivially.
      It now asserts a refusal happened before checking the wording)

### The fence, by hand

- [x] `dotnet run` in Development on an unmarked machine refuses to start — before any
      service is built or port opened, with the server-fixing message and no bypass command
- [x] …and starts normally once the machine is marked — `/admin` loads the full admin page
      and the menu shows its link
- [x] A published Release copy refuses Development, even with the flag present — the
      author's Mac was marked, the published app read the same user-secrets, and it still
      refused with the Release-build message
- [x] …and still starts normally in Production — `/about` loads, `/admin` is 404

### By hand

- [x] A published build contains no `App_Data/` — checked in a clean `dotnet publish` output
- [x] That published app, started in Production with no bank, refuses to start and names
      the missing `teasers.json` — before serving a single page
- [x] The app started in Production answers `/admin` with the not-found page — `/admin`,
      `/admin/anything` and `/ADMIN` all 404 with the site's not-found page, while
      `/about` still loads
- [x] …and navigating to `/admin` from inside the running app does too — checked in the
      published copy, where the framework's JavaScript loads: the address bar changed, the
      page showed not-found with no form or Save button, and the browser never asked the
      server for `/admin`, so the router's check is what stopped it
- [x] …and neither menu links to `/admin` — in Development the page still loads when the
      address is opened directly

### Not covered

- [ ] The publish command — waits on the choice of host ([PRD 11](11-deployment.md))
- [ ] Copying live data down to the Mac — not scripted

## Implementation notes

| File | Role |
|---|---|
| `Services/DevelopmentModeGuard.cs` | Refuses to start in Development unless it's a Debug build on a marked machine |
| `Services/AdminAccess.cs` | The rule, and the request gate |
| `Components/Routes.razor` | The router check, for in-app navigation |
| `Components/Layout/MainLayout.razor` | No admin link in the menu, in any mode |
| `Services/TeaserStore.cs` | Refuses to start on a missing or unreadable bank |
| `Program.cs` | Runs the Development-mode fence first; loads the bank at startup; registers the gate after the not-found re-execution |
| `OneADay.csproj` | Keeps `App_Data/` out of build and publish output |
| `.claude/launch.json` | `oneaday-published` runs a published copy in Production on port 5179 — publish to `OneADay/bin/publish` first. (`dotnet run` in Production can't serve the framework's JavaScript, so only a published copy behaves like the live site.) `guard-check-published-in-development` runs the same copy in Development and must refuse to start. |

> **Found while building.** The router's first version rendered `<Pages.NotFound />`.
> Razor can't read a dotted tag name as a component, so it printed an empty HTML element:
> an in-app visit to `/admin` on the live site would have shown a blank page. The routing
> test caught it; the fix renders the page through `DynamicComponent`.

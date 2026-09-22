# PRD 06 — Community feedback & statistics

**Status:** Spec of record · **Routes:** `/contact`, `/` (issue button), `/admin`

## In plain terms

With no accounts and no comment threads, the site still needs:

- **Statistics** — how you did against everyone else, shown only after you solve, because a low
  success rate seen first just discourages people.
- **Suggestions** — puzzle ideas sent through a form, so the author needn't publish an email
  address.
- **Issue reports** — a way to flag a question that's broken or marking people wrong unfairly.

All three work anonymously and must reach the author reliably. The hard part is keeping out spam
without making real people prove they're human — no puzzles to solve, no boxes to tick, only
checks that run invisibly.

---

## Statistics

- Per teaser, just two numbers: **total answers** and **correct answers**, shown as *"342 out of
  1,208 total submissions were correct"*. Nothing about who answered is stored — no ID, name, email
  or IP address. *(Author's decision, 2026-09-22.)*
- **The cost:** there's no count of people. Someone who answers five times counts five times, and
  since re-solving is allowed on purpose, each re-solve is another correct answer — so the success
  rate reads somewhat high. Accepted.
- The numbers are **all-time** for the teaser, so a recycled teaser carries its earlier runs. That's
  why the line says "total", never "today".
- **Hidden until that solver has solved it.** Beforehand, a low success rate discourages and gives
  away the difficulty; afterwards it reads as a reward.
- The author always sees every teaser's statistics in admin. Deleting a teaser deletes them.

*History: until 2026-09-22 the page also counted people — "🧠 466 minds have taken on this
challenge" — by storing an anonymous ID for everyone who tried each teaser. The IDs were never
pruned and every answer rewrote the whole file, so saving an answer got slower for as long as the
site ran (roughly 9 MB of IDs a year at 500 visitors a day). Replaced by two numbers that grow only
with the number of teasers.*

**Non-goals:** personal history, profiles, scores, median attempts, time-to-solve, percentile
rankings.

---

## Teaser suggestions (`/contact`)

- **No published email address** — a form instead, with three fields: **difficulty**, **the
  teaser**, and **solution + hint**. The last two are required (Submit stays disabled until both
  are filled) and capped at 600 characters.
- After sending: a thank-you, and nothing more offered that day. A visitor over the limit sees a
  friendly explanation instead of the form.
- In admin, suggestions show difficulty, time and text, and can be **copied straight into the
  add-teaser form** or deleted.
- Each also **emails the author** ([PRD 14](14-email-notifications.md)), after saving so it can
  never affect the save. Admin is the record; the email is the nudge.

### Abuse defences

The goal is **one suggestion per person per day, as far as possible** — perfect enforcement is
impossible without accounts. These layers make spamming cost more than it's worth. **None costs a
real visitor a click.**

| Layer | What it is | What it stops |
|---|---|---|
| **0. The connection** | Built in, free. The site runs over a live connection, not one-off web requests, so a spammer can't fire off a simple scripted request — they need a real browser under automation | Casual scripted abuse |
| **1. Silent checks** | A decoy field a person never sees, and a **5-second** minimum between the form appearing and being sent | Naive bots, instantly |
| **2. One per device per day** | Counted by the anonymous device ID | The casual double-send |
| **3. Three per IP address per day** | The real ceiling; addresses are stored only scrambled (below) | Sustained abuse from one address |

A side effect of layer 0: ordinary rate-limiting tools do nothing here, because there's no separate
web request per submission for them to see.

**Layer 1** (`Models/SubmissionGuard.cs`) **rejects silently** — the visitor sees the normal
thank-you, because telling a bot it was caught only teaches its author which check to get round.

- The decoy must **not** be hidden the usual ways (`display:none`, `visibility:hidden`,
  `type="hidden"`): bots skip exactly those fields. It sits off-screen instead — still "visible" to
  a bot reading the page — skipped by the keyboard and hidden from screen readers.
- The 5-second clock is kept **on the server**, so the browser can't read or backdate it.
- **Spaces alone in the decoy don't count**, so a stray autofilled space never costs a real person
  their suggestion.
- **An unstarted clock lets the submission through** — a filter that silently eats real
  submissions is worse than one that misses a bot.
- It runs *before* the limits, so a blocked submission **never uses up anyone's allowance** — a bot
  can't exhaust a household's quota on its way to being rejected.

**Layer 3** is three, not one, because households, offices, schools and mobile networks share IP
addresses; a limit of one would lock out the second person in a family. It's enforced **on the
server** (in `TryAdd`), so skipping the page doesn't skip the check, and its log is kept **apart
from the inbox**, so deleting a spam suggestion doesn't give its sender a fresh slot.

| An attacker who… | Outcome |
|---|---|
| Submits twice | Blocked (layer 2) |
| Clears storage / goes incognito | Reaches 3, then blocked (layer 3) |
| Fills every field with a script | Blocked instantly (layer 1, decoy) |
| Pastes and submits at machine speed | Blocked instantly (layer 1, timing) |
| Scripts a real browser at human pace, from one address | Capped at 3 a day |
| Keeps switching VPN addresses | **Unlimited** |

Admin shows a **running count of blocked submissions** — the only way to tell "no bots are trying"
from "real suggestions are being quietly eaten".

### Known limitations

- **Someone who keeps changing IP address still gets through.** True of any IP limit; accepted. If
  it's ever actually abused, the next step is Cloudflare Turnstile — a free CAPTCHA, invisible to
  most visitors, with no ad tracking.
- **The way the visitor's IP is read is unsupported** in this kind of page (`IHttpContextAccessor`
  in an interactive component); Microsoft warns it may return stale information. It works today —
  by grace, not by contract.

### Fixed

- **History — behind a proxy, every visitor looked like one** (fixed 2026-09-16), which would close
  the form for everyone after three suggestions a day. See [PRD 11](11-deployment.md) requirement 3.
- **History — stored IP addresses could be worked back out** (fixed 2026-09-16). They were stored as
  a plain one-way scramble (a hash), but IPv4 has only about four billion addresses, so trying them
  all takes minutes — one was reversed by hand in six guesses. `Services/IpHasher.cs` now mixes in
  a secret key (`Privacy__IpHashKey`). Outside development a missing or too-short key **stops the
  app starting**, rather than falling back to the weak version while looking protected. *Shipped
  with the proxy fix on purpose: behind a proxy the stored value was the proxy's address, so fixing
  that alone would have started recording real visitors' addresses under the weak scramble.*

---

## Issue reporting

- A floating **"Report an issue"** button, bottom right, on the **challenge of the day only** — not
  Twenty Four, About, Contact or admin.
- A dialog with exactly three categories (*poorly worded* · *submission not accepted or solution
  incorrect* · *other*) and a required description of up to 1,000 characters, with a live counter.
  Send stays disabled until there's text.
- Each report records **which teaser was on screen** and the page address — an "answer not
  accepted" report is useless without the question.
- Each also **emails the author** ([PRD 14](14-email-notifications.md)). Reports are deliberately
  unlimited, so the emailer has its own daily cap: a burst stops the mail, never the save.

### Triage

- Each report has a **status**, stored as readable text: **New** (default) · **In progress** ·
  **Solved** · **Won't solve** · **Duplicate**. The first two count as open.
- Admin shows a coloured badge, a dropdown to change it, the open count in the section header, when
  it last changed, and an **"Edit that teaser"** shortcut. Open reports sort first; closed ones are
  faded.
- **Merely viewing admin must never change a status.**

> **History — regression:** the status dropdown first set the selection twice — a `value` on the
> dropdown *and* `selected` on the option. The two can disagree and fire a phantom change, silently
> re-tagging reports just by loading admin. Keep `selected` only.

### Bot checks

The same two silent checks as suggestions. **This is not a rate limit, and that's the point:** the
checks ask *"was this sent by a script?"*, never *"has this person sent too many?"* Someone who hits
five genuine problems files five reports.

One difference: **the clock starts when the dialog opens, not when the page loads** — that's when
the form actually becomes usable. People often open it after sitting with a puzzle a long time;
timing from page load would never catch anyone who read for ten minutes, and would hit hardest
someone who spots a problem at once.

Blocked reports have their own count in admin, which matters more here than for suggestions: **a
silently eaten report is a broken question nobody ever hears about.**

### Non-goals

- **Limiting reports.** A solver may genuinely hit several problems at once, and an unfiled report
  is a bug that stays broken. Reaffirmed 2026-08-26: screening out scripts must never become a
  back-door limit on people; `IssueStore.Add` stays unlimited on purpose.
- Replying to reporters — they're anonymous by design.

---

## Acceptance criteria

- [x] Stats hidden before solve, shown after; admin always sees them
- [x] Suggestion form replaces the published email address
- [x] Second suggestion same day refused server-side; IPs stored hashed
- [x] Issue button present on `/` and absent on all other routes (verified per route)
- [x] Reports capture the on-screen teaser
- [x] All five statuses round-trip to JSON; viewing admin leaves status untouched

### Statistics — `StatsStoreTests`

- [x] A wrong answer counts as an attempt only; a right one counts as both (mutation-verified)
- [x] Counts are per teaser, survive a restart (mutation-verified), and go when the teaser is
      deleted
- [x] Answering again counts again — re-solving is intended
- [x] **Nothing about who answered is written** — only each teaser's own ID and its two numbers
- [x] A file from the old version still loads with its counts, and drops its visitor IDs on the
      next save
- [x] In the running app: both statistics lines show the new wording, and the real `stats.json`
      went from 41 visitor IDs to none on its first save, with every count intact

### Bot checks — `SubmissionGuardTests`

- [x] An untouched decoy passes, any content blocks, spaces alone do **not**; either signal alone is
      enough to block
- [x] Under 5 seconds is blocked; exactly 5, minutes or hours are allowed; an unset clock lets the
      submission through rather than eating it

### Blocked counts — `BlockedCounterTests`

- [x] Both counts start at zero, count every block, and survive a restart
- [x] Blocking never writes to the inbox or report list, **never uses up anyone's daily allowance**,
      and never limits how many real reports a person can file
- [x] Both JSON files still load from their **old bare-list format**, keeping every record

### The keyed IP hash — `IpHasherTests`

- [x] The same address always hashes the same way (so the limit still recognises a repeat visitor),
      and different addresses differently
- [x] **The key changes the hash** (mutation-verified — the one that matters: an unkeyed version
      passes every other test here and fails only this); the result isn't the plain unkeyed hash
- [x] No address gives no value, so the store falls back to the per-device limit
- [x] Outside development a missing, blank or too-short key **stops the app starting**, in Staging
      as in Production (mutation-verified); a real key starts, and development starts with no key —
      the control cases; the refusal names the setting to fix

### Proxy headers — `ProxyHeadersTests`, real requests on the local machine

- [x] Switched off, a forwarded address is ignored; a known proxy, a known network, and
      trust-everyone each believe the header
- [x] A sender that isn't a known proxy is ignored — proving that naming proxies **replaces** the
      default trust of the local machine rather than adding to it (mutation-verified)
- [x] Announcing a proxy without trusting one **stops the app starting** (mutation-verified)

### Unreadable stored IDs — `ProtectedStorageTests`

A stored visitor ID the server can't decrypt used to crash the visitor's page — see
[PRD 11](11-deployment.md) requirement 4.

- [x] An ID this server can't decrypt reads as *no ID*, so a new one is issued instead of the page
      dying (mutation-verified); nothing stored also reads as no ID
- [x] **A readable ID still comes back** — the control case, and the one that matters: a helper that
      always returned nothing would pass the tests above while silently treating every returning
      visitor as new (mutation-verified)

### Not covered

- [ ] A real visitor's IP arriving through the **real** proxy on the real host. The trust rules are
      tested above, but only the live deploy proves the host's proxy is the one configured — submit
      from two devices and confirm they count separately ([PRD 11](11-deployment.md))
- [ ] Two defensive branches of `ReadOrDefaultAsync`, which mutation testing showed **no test
      reaches**. They guard against a malformed entry, and are labelled in the code as unverified
      rather than counted as covered

## Implementation notes

`StatsStore`, `SuggestionStore` and `IssueStore` each own one JSON file. The report dialog is
`Components/ReportIssue.razor`; `CurrentTeaserContext` tells it which question is on screen, cleared
on navigating away so a report is never pinned to the wrong question.

The bot checks are split on purpose: `SubmissionGuard` decides, as one pure function of (decoy,
time taken) with no clock or files, so the 5-second edge is testable without waiting;
`Components/Pages/Contact.razor` owns the decoy and the server-side clock; `SuggestionStore` owns
the counts.

> The decoy's CSS carries a warning comment that must stay. `display:none` is the obvious way to
> hide a field and exactly wrong here — every bot skips hidden fields — so "tidying" it would
> quietly disable the trap while it still looked right.

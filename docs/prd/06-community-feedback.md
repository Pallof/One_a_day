# PRD 06 — Community feedback & statistics

**Status:** Spec of record · **Routes:** `/contact`, `/` (issue button), `/admin`

## In plain terms

Three things live here. **Statistics** let a solver see how they did against everyone else,
but only after they've solved it — seeing a low success rate first just discourages people.
**Suggestions** let visitors send in puzzle ideas through a form rather than the author
publishing an email address. **Issue reports** let someone flag a question that's broken or
being marked wrong unfairly.

All three work without accounts. The hard part is keeping out spam without making real
people prove they're human — so there are no puzzles to solve and no boxes to tick, just
checks that run invisibly.

## Problem

With no accounts and no comment threads, the product still needs a way for solvers to
contribute ideas and a way to report a broken question. Both must work anonymously, reach
the author reliably, and resist spam. Separately, solvers want to know how they did —
without that spoiling the puzzle.

---

## Statistics

- Track per teaser: **unique attempters**, **total submissions**, **successful submissions**.
- Uniqueness is keyed on an **anonymous per-device ID** (a random GUID in protected browser
  storage). No names, emails, IPs, or personal data.
- Solvers see *"🧠 466 minds have taken on this challenge — 466/13,502 successful attempts"*.
- **Hidden until that solver has solved it.** Seeing a low success rate beforehand
  discourages people and leaks difficulty; afterwards it reads as a reward.
- The author sees stats for every teaser at all times in admin.
- Numbers get thousands separators. Deleting a teaser deletes its stats.

**Non-goals:** per-solver history, profiles, scores, median attempts, time-to-solve,
percentile ranking.

---

## Teaser suggestions (`/contact`)

- The page must **not publish an email address**; it offers a form instead.
- Three inputs: **difficulty** (Easy / Medium / Hard), **the teaser**, and **solution +
  hint**. The last two are required, submit stays disabled until both are filled, and text
  is capped at 600 characters.
- On success, a thank-you state and nothing further offered that day. A limited visitor sees
  a friendly explanation, not the form.
- Suggestions appear in admin with difficulty, timestamp and text, and can be **promoted
  straight into the add-teaser form** or deleted.
- A new suggestion also **emails the author** ([PRD 14](14-email-notifications.md)), sent
  after the save so it can never affect it. Admin is the record; the email is just the nudge.

### Abuse defences

The goal is **one suggestion per person per day, to the best of our ability.** Perfect
enforcement is impossible without accounts, which the product deliberately doesn't have
([PRD 00](00-product-overview.md)). These layers raise the cost of spamming until it isn't
worth anyone's time. **No layer costs a real visitor a click** — there is deliberately no
puzzle to solve.

| Layer | What it is | What it stops |
|---|---|---|
| **0. The transport** | Free, inherent. Because the app runs on a live connection rather than ordinary web requests, submitting isn't a simple HTTP POST — a spammer can't use `curl`, they need a real browser under automation | Casual scripted abuse, at no cost |
| **1. Silent checks** | A decoy field a person never sees, and a **5-second** floor between the form becoming usable and submission | Naive bots, instantly |
| **2. One per device per day** | A random GUID in protected browser storage | The casual double-submit |
| **3. Three per hashed IP per day** | The real ceiling | Sustained abuse from one address |

**A corollary of layer 0 worth knowing:** ordinary HTTP rate-limiting middleware does
nothing here, because there's no per-submission HTTP request for it to see.

**Layer 1 details.** Both checks live in `Models/SubmissionGuard.cs` and both **reject
silently** — the visitor sees the ordinary thank-you, because telling a bot it was caught
only teaches its author which check to route around.

- The decoy must **not** use `display:none`, `visibility:hidden`, or `type="hidden"` —
  scrapers skip all three, which would make the trap useless. It sits off-canvas instead, so
  it stays "visible" to anything reading the page, with `tabindex="-1"` and `aria-hidden`
  covering keyboard and screen-reader users.
- The 5-second timestamp is held **server-side**, so a client can neither read nor backdate it.
- **Whitespace in the decoy doesn't count as filled**, so a stray autofill space never costs
  a real person their suggestion.
- **An unset timestamp must fail open.** A filter that silently eats real submissions is
  worse than one that misses a bot.

Because layer 1 runs *before* the caps, a blocked submission **never consumes anyone's
quota** — a bot can't exhaust a household's allowance on its way to being rejected.

**Layer 3 details.** Three, not one, because households, offices, schools and mobile
carriers legitimately share a public IP and a cap of one would lock out the second person in
a family. Two properties must hold: enforcement is **server-side inside `TryAdd`**, so a
client that skips the UI meets the same check; and the limit log is stored **separately from
the inbox**, so deleting a spam suggestion doesn't hand its sender a fresh slot.

### What this actually stops

| Attacker does | Outcome |
|---|---|
| Submits twice | Blocked (layer 2) |
| Clears storage / incognito | Reaches 3, then blocked (layer 3) |
| Fills every field with a script | Blocked instantly (layer 1, decoy) |
| Pastes and submits at machine speed | Blocked instantly (layer 1, timing) |
| Scripts a real browser, human-paced, one IP | Capped at 3/day |
| Rotates VPN exits | **Unlimited** |

### Observability

The silent checks report a **running blocked count in admin**. Not decoration: a filter that
rejects invisibly gives no way to tell "no bots are trying" apart from "real suggestions are
being quietly eaten".

### Known limitations

- **A determined attacker rotating IPs still gets through.** True of IP limits generally, and
  accepted. Escalation path if it's ever actually abused: a CAPTCHA (Cloudflare Turnstile —
  free, invisible to most visitors, no ad-tech tracking).
- **`IHttpContextAccessor` in an interactive component is unsupported by design.** It works
  today, but Microsoft documents it as unsafe and it may return a stale context. Working by
  grace, not by contract.

### Resolved

- ~~**The IP layer misbehaves behind a reverse proxy.**~~ **Fixed 2026-09-16.** Behind a
  proxy every visitor collapsed into one hash, which would close the form for everyone after
  three submissions a day. `Services/ProxyOptions.cs` now reads forwarded headers, off by
  default, and refuses to start if switched on without naming a trusted source. See
  [PRD 11](11-deployment.md) requirement 3.
- ~~**The stored IP hash is reversible.**~~ **Fixed 2026-09-16.** A plain hash of an IPv4
  address covers only 2³² values and inverts in minutes — one was reversed by hand in six
  guesses. `Services/IpHasher.cs` now uses a keyed hash, so inverting it means guessing the
  key. The key is a secret (`Privacy__IpHashKey`); outside Development a missing or
  token-length one **refuses the start** rather than falling back and looking protected.
  *Shipped together with the fix above on purpose: behind a proxy the stored value was the
  proxy's address, so fixing that alone would have started recording real visitor addresses
  under the weak hash.*

---

## Issue reporting

- A floating **"Report an issue"** button, bottom-right, on the **Challenge of the day page
  only** — not on Twenty Four, About, Contact, or admin.
- The dialog has a dropdown with exactly three options (*poorly worded* · *submission not
  accepted or solution incorrect* · *other*) and a required description, max 1000 characters
  with a live counter. Send stays disabled until it has content.
- Each report automatically captures **which teaser was on screen** plus the page URL — an
  "answer not accepted" report is useless without the question.
- A report also **emails the author** ([PRD 14](14-email-notifications.md)). Because reports
  are deliberately uncapped, the notifier carries its own daily send cap — a burst stops the
  mail, never the save.

### Triage

- Every report carries a **status**, stored as readable text: **New** (default) · **In
  progress** · **Solved** · **Won't solve** · **Duplicate**. The first two count as open.
- Admin shows a colour-coded badge, a dropdown to change status, the open count in the
  section header, a `StatusUpdatedAt` timestamp, and an **"Edit that teaser"** shortcut.
- Open reports sort above closed ones; closed ones are dimmed.
- **Merely viewing admin must never change a status.**

### Anti-automation

The same two silent checks as the suggestion form. **This is not a rate limit, and the
distinction is the whole point:** the checks ask *"was this submitted by a script?"*, never
*"has this person sent too many?"* Someone who hits five genuine problems files five reports.

One difference that matters: **the clock starts when the dialog opens, not on page load.**
The dialog is reached on demand, often after sitting with a puzzle a long time. Anchoring to
page load would mean the check never fires for someone who read for ten minutes, and fires
hardest on someone who spots a problem immediately. Dialog-open is when the form actually
becomes available, so it's the honest anchor.

Blocked reports are counted separately in admin. This matters more here than on the
suggestion form: **a silently eaten report is a broken question nobody ever hears about.**

### Non-goals

- **Rate limiting reports.** A solver may legitimately hit several issues in one sitting, and
  an unfiled report is a bug that stays broken. Reaffirmed 2026-08-26: screening out scripts
  must never become a back-door cap on people, and `IssueStore.Add` stays uncapped on purpose.
- Replying to reporters — they're anonymous by design.

---

## Acceptance criteria

- [x] Stats hidden before solve, shown after; admin always sees them
- [x] Suggestion form replaces the published email address
- [x] Second suggestion same day refused server-side; IPs stored hashed
- [x] Issue button present on `/` and absent on all other routes (verified per-route)
- [x] Reports capture the on-screen teaser
- [x] All five statuses round-trip to JSON; viewing admin leaves status untouched

### Anti-automation — `SubmissionGuardTests`

- [x] An untouched decoy lets the submission through; any content blocks it; whitespace alone
      does **not**
- [x] Faster than 5 seconds is blocked, exactly 5 is allowed, minutes or hours never are
- [x] An unset timestamp fails open rather than eating the submission
- [x] Either signal alone is enough to block

### Blocked counters — `BlockedCounterTests`

- [x] Both counters start at zero, count every block, and survive a restart
- [x] Blocking never writes to the inbox or triage list
- [x] Blocking a suggestion **does not consume anyone's daily quota**
- [x] Blocking reports never caps how many real reports a person can file
- [x] Both JSON files in their **old bare-array format still load**, keeping every record

### The keyed IP hash — `IpHasherTests`

- [x] The same address always hashes the same way, so the cap still recognises a repeat visitor
- [x] Different addresses hash differently
- [x] **The key changes the hash** (mutation-verified — the load-bearing one: an unkeyed
      implementation passes every other test here and fails only this)
- [x] The result is not the plain unkeyed hash, guarding that regression directly
- [x] No address at all hashes to null, so the store falls back to the per-device limit
- [x] Outside Development a missing, blank or token-length key **refuses the start**, in
      Staging as well as Production (mutation-verified)
- [x] A real key starts, and Development starts with no key — the control cases
- [x] The refusal names the environment variable to set

### Forwarded headers — `ProxyHeadersTests`, real requests on a loopback port

- [x] Switched off, a forwarded address is ignored
- [x] A sender that isn't a known proxy is ignored — which also pins that explicit
      configuration **replaces** the built-in trust of loopback rather than adding to it
      (mutation-verified)
- [x] A known proxy, a known network, and trust-everything each believe the header
- [x] Announcing a proxy without trusting one **refuses the start** (mutation-verified)

### Unreadable stored values — `ProtectedStorageTests`

The visitor id lives in protected browser storage, and reading it **throws** rather than
reporting failure when the value can't be decrypted. Unhandled, that kills the visitor's
connection and their page stops working. Found 2026-09-16 by moving the Data Protection key
ring ([PRD 11](11-deployment.md) requirement 4), which made every browser holding an older
value hit it at once.

- [x] A value this server can't decrypt reads as *no value*, so a new id is minted instead of
      the page dying (mutation-verified)
- [x] Nothing stored reads as no value
- [x] **A readable value still comes back** — the control case, and load-bearing: a helper
      that always returned nothing would pass both tests above while silently giving every
      returning visitor a fresh identity (mutation-verified)

### Not covered

- [ ] A real client IP arriving through a **real** proxy on the real host. The trust rules are
      covered above, but only the live deploy proves the host's proxy is the one configured —
      submit from two devices and confirm they count separately ([PRD 11](11-deployment.md))
- [ ] Two defensive branches of `ReadOrDefaultAsync`. Mutation testing showed **neither is
      reached** by any test. They're breadth against a malformed entry, not verified
      behaviour, and are labelled as such in the source rather than counted as covered

> **Regression on record:** the status dropdown initially set both a `value` attribute *and*
> `selected` on its options. Two sources of truth can disagree and fire a spurious change
> event, silently re-tagging issues just from loading admin. Keep `selected` only.

## Implementation notes

`StatsStore`, `SuggestionStore`, `IssueStore` (all singletons over JSON files);
`Components/ReportIssue.razor` for the dialog; `CurrentTeaserContext` is how `ChallengeView`
tells the dialog which question is on screen, cleared on navigate-away so a report is never
mis-attributed.

The anti-automation checks are split deliberately: `Models/SubmissionGuard.cs` holds the
decision as one pure function over `(decoy, elapsed)` — no clock, no I/O, so the 5-second
boundary is testable without waiting — while `Pages/Contact.razor` owns the decoy markup and
the server-side timestamp, and `SuggestionStore` owns the counts.

> The decoy's CSS carries a warning comment that needs to stay. `display:none` is the obvious
> way to hide a field and exactly wrong here: every scraper skips hidden inputs, so the "fix"
> would quietly disable the trap while leaving it looking correct.

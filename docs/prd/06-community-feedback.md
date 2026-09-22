# PRD 06 — Community feedback & statistics

**Status:** Spec of record · **Routes:** `/contact`, `/` (issue button), `/admin`

## Problem

With no accounts and no comment threads, the product still needs two channels: a way
for solvers to contribute puzzle ideas, and a way to report a question that is broken
or being marked wrong unfairly. Both must work anonymously, reach the author reliably,
and resist spam. Separately, solvers want to know how they did relative to others —
without that information spoiling the puzzle.

---

## Statistics

### Requirements

- Track per teaser: **unique attempters**, **total submissions**, **successful
  submissions**.
- Uniqueness is keyed on an **anonymous per-device ID** (a random GUID in protected
  browser storage). No names, emails, IPs, or personal data.
- Solvers see a line of the form
  *"🧠 466 minds have taken on this challenge — 466/13,502 successful attempts"*.
- **The line is hidden until that solver has solved the puzzle.** Seeing a low
  success rate beforehand discourages people and leaks difficulty; afterwards it
  reads as a reward.
- The author sees stats for every teaser at all times in admin.
- Numbers format with thousands separators.
- Deleting a teaser deletes its stats.

### Non-goals

- Per-solver history, profiles, or scores
- Median attempts, time-to-solve, or percentile ranking

---

## Teaser suggestions (`/contact`)

### Requirements

- The contact page must **not publish an email address**; it offers a form instead.
- Three inputs: **difficulty** (Easy / Medium / Hard), **the brain teaser**, and
  **solution + hint**. The latter two are required; submit stays disabled until both
  are filled. Text inputs capped at 600 characters.
- On success, show a thank-you state and offer nothing further that day.
- A limited visitor sees a friendly explanation, not the form.
- Suggestions appear in admin with difficulty, timestamp, and text, and can be
  **promoted straight into the add-teaser form** or deleted.
- A new suggestion also **emails the author** ([PRD 14](14-email-notifications.md)).
  The mail is sent after the save and can never affect it — admin remains the record,
  the email is only the nudge to go and look.

### Abuse defences

The goal is **one suggestion per person per day, to the best of our ability** — worth
stating plainly that perfect enforcement is impossible without accounts, which the
product deliberately doesn't have ([PRD 00](00-product-overview.md)). What follows
raises the cost of spamming until it isn't worth anyone's time.

Four layers, in the order a submission meets them. **No layer costs a real visitor a
click** — there is deliberately no puzzle to solve.

#### 0. The transport (free, inherent)

Because the app is Blazor Server, submitting is **not an HTTP POST** — it is a sequence
of messages on a SignalR circuit. A spammer cannot use `curl`; they need a real browser
under automation or a client that speaks Blazor's circuit protocol. This is a
substantial barrier that comes for free with the architecture, and it is worth knowing
about before reaching for heavier tools. Note the corollary: **ordinary HTTP rate-limit
middleware does not apply here**, because there is no per-submission HTTP request for it
to see.

#### 1. Silent automation checks

Both live in `Models/SubmissionGuard.cs`, and both **reject silently** — the visitor
sees the ordinary thank-you. Telling a bot it was caught only teaches its author which
check to route around.

- **Honeypot.** A decoy input a person never sees. It must **not** use `display:none`,
  `visibility:hidden`, or `type="hidden"` — scrapers skip all three, which would make
  the trap useless. It is moved off-canvas instead, so it stays "visible" to anything
  reading the DOM, with `tabindex="-1"` and `aria-hidden="true"` covering keyboard and
  screen-reader users. Any content in it means a script filled it in.
- **Time to submit.** A floor of **5 seconds** between the form becoming interactive and
  the submission. Nobody composes a riddle *and* its solution faster than that; anything
  that does already had the text. The timestamp is held **server-side in the circuit**,
  so the client can neither read it nor backdate it.

Two rules for this layer:

- **Whitespace in the honeypot does not count as filled**, so a stray autofill space
  never costs a real person their suggestion.
- **An unset timestamp must fail open.** A filter that silently eats real submissions is
  worse than one that misses a bot.

Because these run *before* the daily caps, a blocked submission **never consumes anyone's
quota** — so a bot cannot exhaust a shared household's allowance on its way to being
rejected.

#### 2. One per device per day

A random GUID in `ProtectedLocalStorage` (`oad-visitor-id`). Protected storage is
encrypted with the server key, so a visitor cannot forge one or impersonate another —
but they can **delete** it, and the app then mints a fresh one. This layer stops the
casual double-submit and nothing more.

#### 3. Three per hashed IP per day

The real ceiling. Deliberately **greater than one**: households, offices, schools, and
mobile carriers legitimately share a public IP, and a cap of one would lock out the
second person in a family.

Two properties that must hold:

- Enforcement is **server-side inside `TryAdd`**, under the lock — not merely a hidden
  button. A client that skips the UI meets the same check.
- The limit log is stored **separately from the inbox**, so deleting a spam suggestion
  in admin does not hand its sender a fresh slot.

### What this actually stops

| Attacker does | Outcome |
|---|---|
| Submits twice | Blocked (layer 2) |
| Clears storage / incognito | Reaches 3, then blocked (layer 3) |
| Fills every field with a script | Blocked instantly (layer 1, honeypot) |
| Pastes and submits at machine speed | Blocked instantly (layer 1, timing) |
| Scripts a real browser, human-paced, one IP | Capped at 3/day |
| Rotates VPN exits | **Unlimited** |

### Observability

The silent checks report a **running blocked count in admin**. This is not decoration: a
filter that rejects invisibly gives no way to tell "no bots are trying" apart from
"real suggestions are being quietly eaten". The count is what makes the difference
visible.

### Known limitations and open risks

- **A determined attacker rotating IPs still gets through.** True of IP limits
  generally, and accepted. Escalation path if it is ever actually abused: a CAPTCHA
  (Cloudflare Turnstile — free, invisible to most visitors, no ad-tech tracking).
- ~~**The IP layer will misbehave behind a reverse proxy.**~~ **Fixed 2026-09-16.**
  `RemoteIpAddress` is whoever connected to Kestrel; behind nginx, Cloudflare, or Azure
  App Service that is *the proxy*, collapsing every visitor into one hash — after three
  submissions the form would close for everyone, every day. `Services/ProxyOptions.cs`
  now reads the forwarded headers, **off by default** because the author's machine has no
  proxy. Switching it on without naming a trusted source **refuses the start**: the
  middleware would otherwise trust only loopback, ignore every header in silence, and
  leave the failure in place with no error to chase. See [PRD 11](11-deployment.md).
- ~~**The stored IP hash is reversible.**~~ **Fixed 2026-09-16.** Plain SHA-256 over an IP
  is not pseudonymisation: IPv4 is only 2³² values, so the whole keyspace is minutes of GPU
  work, and a localhost hash was reversed by hand in six guesses. `Services/IpHasher.cs`
  now uses HMAC-SHA256 with a server-side key — the same determinism, so the cap is
  unchanged, but inverting it means guessing 256 bits rather than walking four billion.

  The key is a secret: user-secrets locally (`Privacy:IpHashKey`), `Privacy__IpHashKey` in
  production, never `appsettings.json`. **Outside Development a missing or token-length key
  refuses the start**, rather than falling back to an unkeyed hash and looking protected
  while storing recoverable addresses.

  These two shipped together deliberately. Behind a proxy without forwarded headers the
  stored value is a hash of *the proxy's* address, which reveals nothing; fixing that alone
  would have started recording real visitor addresses under the weak hash. Migration was
  nothing: the cap only compares entries from **today** (`ReachedLimit`), so old unkeyed
  hashes stop matching for at most the rest of one day, and the 7-day prune clears them.
  The same holds if the key is ever rotated.
- **`IHttpContextAccessor` in an interactive component is unsupported by design.** It
  works today (the context flows into the circuit), but Microsoft documents it as
  unsafe in interactive Blazor components, and it may return a stale or disposed
  context. It is working by grace, not by contract.

---

## Issue reporting

### Requirements

- A floating **"Report an issue"** button, bottom-right, on the **Challenge of the
  day page only** — not on the Twenty Four, About, Contact, or admin pages.
- Opens a dialog containing:
  - a **dropdown** with exactly three options:
    1. *Question is poorly worded or written incorrectly*
    2. *Submission not accepted or being evaluated correctly, or solution is incorrect*
    3. *Other*
  - a **description textarea** (required, max 1000 chars, live counter). Send stays
    disabled until it has content.
- Each report must automatically capture **which teaser was on screen** plus the page
  URL — an "answer not accepted" report is useless without knowing the question.
- On success, show a confirmation.
- A report also **emails the author** ([PRD 14](14-email-notifications.md)). Because
  reports are deliberately uncapped, the notifier carries its own daily send cap — a
  burst stops the mail, never the save.

### Triage

- Every report carries a **status tag** persisted in `issues.json` as readable text:
  **New** (default) · **In progress** · **Solved** · **Won't solve** · **Duplicate**.
- New and In progress count as *open*; the other three are closed out.
- Admin shows a colour-coded status badge, a dropdown to change status, the open
  count in the section header, a `StatusUpdatedAt` timestamp, and an **"Edit that
  teaser"** shortcut jumping to the reported question.
- Open reports sort above closed ones; closed ones are visually dimmed.
- **Merely viewing the admin page must never change a status.**

### Anti-automation

The report dialog runs the **same two silent checks as the suggestion form** — the
off-canvas decoy field and the 5-second floor, both from `SubmissionGuard`.

**This is not a rate limit, and the distinction is the whole point.** The checks ask
*"was this submitted by a script?"*, never *"has this person sent too many?"* A person
who hits five genuine problems in one sitting files five reports, exactly as before.

One difference from the suggestion form, and it matters:

> **The clock starts when the dialog opens, not on page load.** The report dialog is a
> modal reached on demand, often after someone has sat with a puzzle for a long time.
> Anchoring to page load would mean a visitor who reads for ten minutes and *then*
> notices a problem is measured against a ten-minute-old timestamp — the check would
> never fire for them, and would fire hardest on someone who spots a problem
> immediately. Dialog-open is the moment the form actually becomes available, so it is
> the honest anchor.

Blocked reports are counted and surfaced in admin, separately from the suggestion
count. This matters more here than on the suggestion form: **a silently eaten report is
a broken question nobody ever hears about.**

### Non-goals

- **Rate limiting reports.** A solver may legitimately hit several issues in one
  sitting, and a report that goes unfiled is a bug that stays broken. Reaffirmed
  2026-08-26 when the anti-automation checks were added to this dialog: screening out
  scripts must never become a back-door cap on people, and `IssueStore.Add` stays
  uncapped on purpose.
- Replying to reporters (they're anonymous by design)

---

## Acceptance criteria

- [x] Stats hidden before solve, shown after; admin always sees them
- [x] Suggestion form replaces the published email address
- [x] Second suggestion same day is refused server-side; IPs stored hashed
- [x] Issue button present on `/` and absent on all other routes (verified per-route)
- [x] Reports capture the on-screen teaser
- [x] All five statuses round-trip to JSON; viewing admin leaves status untouched

### Anti-automation — covered by `SubmissionGuardTests`

- [x] An untouched decoy field lets the submission through
- [x] Any content in the decoy blocks it
- [x] Whitespace alone in the decoy does **not** block it
- [x] Submissions faster than 5 seconds are blocked; 5 seconds exactly is allowed
- [x] A visitor taking minutes or hours is never blocked
- [x] An unset timestamp fails open rather than eating the submission
- [x] Either signal alone is enough to block

### The blocked counters — covered by `BlockedCounterTests`

- [x] Both counters start at zero and count every block
- [x] Both survive a restart (persisted, not in-memory)
- [x] Blocking never writes to the inbox or the triage list
- [x] Blocking a suggestion **does not consume anyone's daily quota**
- [x] Blocking reports never caps how many real reports a person can file
- [x] `issues.json` in the **old bare-array format still loads**, keeping every report,
      and gains the counter on the next write
- [x] `suggestions.json` in its old bare-array format still loads

Verified in the browser against the running app:

- [x] Both decoys render with `display: block` (not `none`) so scrapers still see them,
      while sitting off-canvas at `x = -9999` and unreachable by tab or screen reader
- [x] A scripted fill of every field is dropped on **both** forms, the bot sees the
      ordinary confirmation, nothing is written, and no quota is consumed
- [x] A report sent 1,004 ms after the dialog opened was blocked by the timing floor
- [x] The real `issues.json` migrated from the old format with its report intact
- [x] Both blocked counts surface in admin

### The keyed IP hash — covered by `IpHasherTests`

- [x] The same address always hashes the same way, so the cap still recognises a repeat
      visitor
- [x] Different addresses hash differently
- [x] **The key changes the hash** — mutation-verified, and the load-bearing one: an unkeyed
      implementation passes every other test in this list and fails only this
- [x] The result is not the plain SHA-256 of the address, guarding that regression directly
- [x] No address at all hashes to null, so the store falls back to the per-device limit
      rather than counting every anonymous visitor as one person
- [x] Outside Development a missing, blank or token-length key **refuses the start** — in
      Staging as well as Production (mutation-verified: switching the refusal off fails six)
- [x] A real key starts, and Development starts with no key at all — the control cases
- [x] The refusal names the environment variable to set. Unlike the Development-mode guard,
      naming it here is the fix rather than a bypass

### Forwarded headers — covered by `ProxyHeadersTests`, real requests on a loopback port

- [x] Switched off, a forwarded address is ignored
- [x] A sender that isn't a known proxy is ignored — which also pins that explicit
      configuration **replaces** the middleware's built-in trust of loopback rather than
      adding to it (mutation-verified: appending instead of replacing fails this, and would
      have made the configuration look authoritative when it wasn't)
- [x] A known proxy, a known network, and trust-everything each believe the header
- [x] Announcing a proxy without trusting one **refuses the start**, rather than silently
      ignoring every header and leaving both failures in place (mutation-verified)

### Unreadable stored values — covered by `ProtectedStorageTests`

The visitor id lives in `ProtectedLocalStorage`, and `GetAsync` **throws** rather than
reporting failure when a value can't be decrypted. Unhandled, that kills the visitor's
circuit — their page stops working. Found 2026-09-16 by moving the Data Protection key ring
([PRD 11](11-deployment.md) requirement 4), which made every browser holding an older value
hit it at once.

- [x] A value this server cannot decrypt reads as *no value*, so a new id is minted instead
      of the page dying (mutation-verified: rethrowing makes this fail, and the exception it
      throws is the real one — `CryptographicException`, the same seen in the live log)
- [x] Nothing stored reads as no value
- [x] **A readable value still comes back** — the control case, and load-bearing:
      mutation-verified that a helper always returning `default` fails here while passing
      both tests above. Without it, every returning visitor would silently get a fresh
      identity on every page load

### Not covered

- [ ] A real client IP arriving through a **real** proxy on the real host. The trust rules
      are covered above, but only the live deploy proves the host's proxy is the one
      configured — submit from two devices and confirm they count separately
      ([PRD 11](11-deployment.md))
- [ ] The `FormatException` and `JsonException` branches of `ReadOrDefaultAsync`. Mutation
      testing showed **neither is reached** by any test: rethrowing from either changes no
      result. They are defensive breadth against a malformed or reshaped entry, not verified
      behaviour, and are labelled as such in the source rather than counted as covered

> **Regression on record:** the status `<select>` initially set both a `value`
> attribute *and* `selected` on its options. Those two sources of truth can
> disagree and fire a spurious change event, silently re-tagging issues just from
> loading admin. Keep `selected` only.

## Implementation notes

`StatsStore`, `SuggestionStore`, `IssueStore` (all singletons over JSON files);
`Components/ReportIssue.razor` for the dialog; `CurrentTeaserContext` (scoped) is how
`ChallengeView` tells the report dialog which question is on screen, cleared on
navigate-away so a report is never mis-attributed.

The anti-automation checks are split deliberately: `Models/SubmissionGuard.cs` holds the
decision as one pure function over `(honeypot, elapsed)` — no clock, no I/O, so the
5-second boundary is testable without waiting — while `Pages/Contact.razor` owns the
decoy markup and the server-side timestamp, and `SuggestionStore` owns the counts.

> The decoy's CSS carries a warning comment, and it needs to stay. `display:none` is
> the obvious way to hide a field and it is exactly wrong here: every scraper library
> skips hidden inputs, so the "fix" would quietly disable the trap while leaving it
> looking correct. Off-canvas positioning is the point, not an accident.

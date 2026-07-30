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
- **Rate limit: one suggestion per device per day**, plus **three per hashed IP per
  day** as a bot backstop.
  - IPs are stored **only as SHA-256 hashes**, never raw.
  - The per-IP allowance is deliberately >1 so a shared household or office network
    isn't locked out by one person.
  - Enforcement is **server-side**, not just hidden UI.
  - The limit log is kept **separately from the inbox**, so deleting a suggestion
    does not hand its sender a fresh slot.
- A limited visitor sees a friendly explanation, not the form.
- Suggestions appear in admin with difficulty, timestamp, and text, and can be
  **promoted straight into the add-teaser form** or deleted.

### Known limitation

A determined attacker rotating IPs can still get through; that is true of IP limits
generally. Escalation path if abused: a CAPTCHA on the form.

---

## Issue reporting

### Requirements

- A floating **"Report an issue"** button, bottom-right, on the **Challenge of the
  day page only** — not on archive, question, About, Contact, or admin pages.
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

### Triage

- Every report carries a **status tag** persisted in `issues.json` as readable text:
  **New** (default) · **In progress** · **Solved** · **Won't solve** · **Duplicate**.
- New and In progress count as *open*; the other three are closed out.
- Admin shows a colour-coded status badge, a dropdown to change status, the open
  count in the section header, a `StatusUpdatedAt` timestamp, and an **"Edit that
  teaser"** shortcut jumping to the reported question.
- Open reports sort above closed ones; closed ones are visually dimmed.
- **Merely viewing the admin page must never change a status.**

### Non-goals

- Rate limiting reports (a solver may legitimately hit several issues)
- Replying to reporters (they're anonymous by design)

---

## Acceptance criteria

- [x] Stats hidden before solve, shown after; admin always sees them
- [x] Suggestion form replaces the published email address
- [x] Second suggestion same day is refused server-side; IPs stored hashed
- [x] Issue button present on `/` and absent on all other routes (verified per-route)
- [x] Reports capture the on-screen teaser
- [x] All five statuses round-trip to JSON; viewing admin leaves status untouched

> **Regression on record:** the status `<select>` initially set both a `value`
> attribute *and* `selected` on its options. Those two sources of truth can
> disagree and fire a spurious change event, silently re-tagging issues just from
> loading admin. Keep `selected` only.

## Implementation notes

`StatsStore`, `SuggestionStore`, `IssueStore` (all singletons over JSON files);
`Components/ReportIssue.razor` for the dialog; `CurrentTeaserContext` (scoped) is how
`ChallengeView` tells the report dialog which question is on screen, cleared on
navigate-away so a report is never mis-attributed.

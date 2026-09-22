# PRD 12 — Streaks & sharing

**Status:** Proposed · **Priority: P2 — post-launch retention**

## In plain terms

**Not built yet — this is a proposal.** Nothing brings a solver back tomorrow except memory: the
countdown says *when* to return but gives no reason to care, and nothing gives anyone a reason to
tell a friend. For daily puzzle games, two things reliably do both: a **streak** (days in a row
solved — a personal record you don't want to break) and a **share button** that copies a short,
spoiler-free result (the loop that made Wordle spread).

Both work without accounts: a streak can be kept in your own browser's encrypted storage, as the
site already does for small things. Clearing the browser loses it — the honest cost of having no
logins — so the wording should never imply a streak is permanent.

## Goals

- Give returning solvers a visible, personal reason to come back.
- Give solvers a one-tap way to share a result that spoils nothing.
- Keep the no-accounts principle intact.

## Non-goals

- Leaderboards, or comparing streaks between people
- Accounts, or syncing a streak across devices
- Punishing a missed day beyond resetting the count

## Requirements

### Streaks

1. Track per device: **current streak**, **longest streak**, **total solved**.
2. A day counts when the solver **solves that day's challenge**. Since the archive went
   ([PRD 04](04-archive-and-discovery.md)) that's the only teaser there is — but the rule stays
   explicit so a future archive can't quietly inflate streaks.
3. Consecutive Pacific days add one; a missed day resets the current streak to zero and keeps the
   longest.
4. Show the streak after solving (e.g. *"🔥 6 days in a row — your best is 9"*).
5. Keep it in the browser's encrypted storage, not on the server.
6. Cleared storage means a lost streak, handled without error (a fresh solver).
7. A first-ever solve gets an encouraging first-day message, not "streak: 1".

### Sharing

8. A **Share** button after solving copies a short summary, e.g.
   `Stumpty — 29 Jul 2026 🧠 solved in 2 attempts (🔥6) https://…`
9. It must **never** include the question, the answer, or anything that spoils the puzzle.
10. Confirm the copy visibly ("Copied!").
11. Use the device's own share menu where available, otherwise the clipboard.

## Open questions

- Include the attempt count? It's the interesting part, but might put people off sharing a scrappy
  8-attempt solve. Suggest including it, and revisiting.
- Should revealing a solution break the streak? Moot today — the daily challenge never offers a
  reveal ([PRD 03](03-hints-and-solutions.md)). Revisit only if an archive returns.

## Acceptance criteria

- [ ] Solving on consecutive days increments; skipping a day resets current, keeps longest
- [ ] Only daily-challenge solves affect the streak
- [ ] Streak survives a page reload and a server restart
- [ ] Cleared browser storage is handled without error
- [ ] Share text contains no spoilers and copies with confirmation
- [ ] Day boundaries use Pacific Time, consistent with [PRD 01](01-daily-challenge.md)

## Risks

- **Streaks kept on one device get lost** — a new browser or cleared storage loses progress, and
  solvers may find that unfair.
- A streak can make a daily habit feel like an obligation. Keep the tone light, and no guilt-trip
  nagging.

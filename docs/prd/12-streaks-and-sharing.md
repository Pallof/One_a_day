# PRD 12 — Streaks & sharing

**Status:** Proposed · **Priority: P2 — post-launch retention**

## Problem

Nothing currently brings a solver back tomorrow except memory. The post-solve
countdown tells them *when* to return but gives them no reason to care, and nothing
gives them a reason to tell anyone else. For daily puzzle games, the two mechanics
that reliably drive both are **streaks** (a personal record you don't want to break)
and a **spoiler-free share** (the growth loop that made Wordle spread).

Both are achievable within the no-accounts constraint: the anonymous per-device ID
already used for statistics is enough to track a streak locally.

## Goals

- Give returning solvers a visible, personal reason to come back.
- Give solvers a one-tap way to share a result that spoils nothing.
- Keep the no-accounts principle intact.

## Non-goals

- Global leaderboards or comparing streaks between people
- Accounts, or syncing a streak across a solver's devices
- Punishing missed days beyond resetting the count

## Requirements

### Streaks

1. Track per device: **current streak**, **longest streak**, **total solved**.
2. A day counts toward the streak when the solver **solves that day's challenge** —
   solving an archived puzzle must not extend it (otherwise the archive trivially
   inflates streaks).
3. Solving on consecutive Pacific days increments; a missed day resets the current
   streak to zero and preserves the longest.
4. Show the streak in the post-solve state (e.g. *"🔥 6 days in a row — your best is 9"*).
5. Streak state lives in **protected browser storage**, not on the server.
6. Degrade gracefully: cleared storage means a lost streak, which must be handled
   without error (treated as a fresh solver).
7. First-ever solve shows an encouraging first-day message, not "streak: 1".

### Sharing

8. A **Share** button in the post-solve state copies a short text summary to the
   clipboard, e.g.
   `One a Day — 29 Jul 2026 🧠 solved in 2 attempts (🔥6) https://…`
9. The share text must **never** contain the question, the answer, or anything that
   spoils the puzzle for the recipient.
10. Confirm the copy visibly ("Copied!").
11. Use the native share sheet where available, falling back to clipboard.

## Open questions

- Should the share include attempt count? It's the interesting part, but it may
  discourage sharing after a scrappy 8-attempt solve. Suggest including it, revisit.
- Should a revealed solution break the streak? Proposal: **no** — reveals are only
  possible on past questions, which don't count anyway.

## Acceptance criteria

- [ ] Solving on consecutive days increments; skipping a day resets current, keeps longest
- [ ] Archived solves never affect the streak
- [ ] Streak survives a page reload and a server restart
- [ ] Cleared browser storage is handled without error
- [ ] Share text contains no spoilers and copies with confirmation
- [ ] Day boundaries use Pacific Time, consistent with [PRD 01](01-daily-challenge.md)

## Risks

- **Device-local streaks are lossy** — a new browser or cleared storage loses
  progress, and solvers may find that unfair. It is the honest cost of having no
  accounts; the messaging should avoid implying the streak is permanent.
- Streak pressure can make a daily habit feel like an obligation. Keep the framing
  light and avoid loss-aversion nagging.

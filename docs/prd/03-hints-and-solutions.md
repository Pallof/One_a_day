# PRD 03 — Hints & solutions

**Status:** Spec of record

## Problem

Help has to exist — an unsolvable puzzle with no way forward is just a dead end. But
help that's one frictionless click away turns the puzzle into a reading exercise. The
product needs a middle path: available, but earned.

## Requirements

### Hints

- A hint is optional per teaser; if the author wrote none, no hint UI appears.
- The hint sits **between the question and the submission box**.
- It is **concealed by default**.
- It **unlocks only after the solver's first attempt.** Before that, show a locked
  affordance (*"🔒 Give it a try first — a hint unlocks after your first attempt."*)
  that is visibly not a button.
- Once unlocked, one click reveals it and **clicking the revealed hint hides it
  again** — the same element toggles, with a "Click to hide" affordance.
- Revealing a hint carries no penalty and is not recorded.

### Solutions

- A solution/explanation is optional per teaser.
- On a **correct answer**, the solution is shown immediately (the puzzle is over).
  If the author wrote no solution, no empty container may render.
- **Solutions unlock the day after a challenge runs — never while it is live.**
  A puzzle has to actually be solved on its own day; no amount of effort or
  persistence opens the answer early.
- A **"Reveal solution"** button therefore appears when **both** hold:
  1. the teaser's date is **before today** (Pacific), and
  2. the solver has not already solved it.

  There is deliberately **no attempt threshold** — effort is not what earns the
  answer, time is.
- Revealing shows the primary accepted answer plus the explanation, and locks the
  submission box.
- On a **live** challenge, once the solver has attempted at least once, show a note
  setting expectations (*"🔒 No peeking today — the solution unlocks tomorrow."*)
  rather than leaving them hunting for a button that isn't there.
- On a past challenge the retry message signposts the option
  (*"Not quite — try again, or reveal the solution."*).

### Spoiler protection

- Today's challenge must **never** offer a solution reveal, however many attempts
  are made.
- Scheduled (future) teasers must never offer it either, even when viewed directly
  by URL.

## Non-goals

- Multiple progressive hints per puzzle
- Scoring or penalising hint/solution use
- Remembering hint state across page loads

## Acceptance criteria

- [x] Hint locked before the first attempt, unlocked after it
- [x] Revealed hint re-conceals on click
- [x] Reveal button available immediately on a past challenge, with no attempt
      threshold
- [x] Reveal button **never** appears on a live challenge, however many attempts
      are made (verified with 4 failed attempts on a teaser dated today)
- [x] Live challenge shows the "unlocks tomorrow" note after the first attempt
- [x] Scheduled (future) teasers never reveal
- [x] Solved teasers with no written solution render no empty panel

## Implementation notes

`Components/ChallengeView.razor` holds the hint gate as `MinAttemptsBeforeHint = 1`.
The reveal is gated solely by the `CanReveal` parameter, which callers set to
`Teaser.Date < today` — so it is a function of the calendar, not of effort. Both the daily page and archived question pages use this same
component, so the rules cannot diverge between them.

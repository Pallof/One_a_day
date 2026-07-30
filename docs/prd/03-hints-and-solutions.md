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
- A **"Reveal solution"** button appears only when **all** of these hold:
  1. the teaser is a **past** teaser (not today's, not scheduled),
  2. the solver has made **at least 3 attempts**,
  3. they have not already solved it.
- Revealing shows the primary accepted answer plus the explanation, and locks the
  submission box.
- After the third failed attempt the retry message must change to signpost the
  option (*"Not quite — try again, or reveal the solution."*).

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
- [x] Reveal button hidden at attempts 1–2, shown at 3, on past questions only
- [x] Reveal button never appears on today's or a scheduled teaser (verified with
      3 failed attempts on a future-dated question)
- [x] Solved teasers with no written solution render no empty panel

## Implementation notes

`Components/ChallengeView.razor` holds both gates as constants —
`MinAttemptsBeforeHint = 1` and `MinAttemptsBeforeReveal = 3` — and the reveal is
additionally gated by the `CanReveal` parameter, which callers set to
`Teaser.Date < today`. Both the daily page and archived question pages use this same
component, so the rules cannot diverge between them.

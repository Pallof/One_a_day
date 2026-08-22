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
  1. the teaser is **not the one currently running**, and
  2. the solver has not already solved it.

  There is deliberately **no attempt threshold** — effort is not what earns the
  answer, time is.

  Note that "not currently running" is *not* the same as "dated in the past".
  Recycling means an old teaser can be today's live challenge, so an age test would
  hand over the live answer.
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
The reveal is gated solely by the `CanReveal` parameter — so it is a function of what
is live, not of effort.

> Since [PRD 04](04-archive-and-discovery.md) removed the archive, the daily page is
> the only caller, and it passes `CanReveal="false"` unconditionally: the Challenge
> of the day never gives its answer away. This is also what keeps recycling safe — a
> recycled teaser is dated in the past while being the live puzzle, so any future
> caller must decide from the schedule, never from `Teaser.Date`.

### Yesterday's solution (`/yesterday`)

With no archive, this is the one place a solution is published.

**It publishes whichever teaser actually ran yesterday — not the teaser dated
yesterday.** Once the recycling box starts drawing old questions back out
([PRD 08](08-recycling-rotation.md)), a teaser's date says nothing about when it was
last shown: a puzzle written in March can be the live challenge in August. The page
therefore resolves the previous day through the same scheduler as any other day, and
reads the answer off *that* teaser.

1. Resolve today's challenge, then resolve the day before it. Today is settled first
   so the result does not depend on which page a visitor happens to open, and so
   yesterday's draw sees today's in the rotation cooldown.
2. Publish the teaser that came back for the previous day.

> **Regression on record:** an earlier version consulted rotation history but fell
> back to a *date-based* lookup whenever that history was thin — which is every fresh
> deploy and any gap in visits. The fallback returned the second-newest teaser the
> author had typed in, so the page stayed pinned to hand-scheduled content and barely
> changed between uploads, exactly the opposite of the daily churn the rotation
> exists to provide. Resolving through the scheduler is what makes it question-based
> rather than date-based; do not reintroduce a date fallback.

#### Never publish the live answer

If the previous day resolves to **the same teaser as today**, the page must show
nothing rather than the solution — publishing it would hand over the answer to the
challenge people are currently solving.

This is reachable only with a bank of **fewer than five** teasers, where the rotation
cooldown (`floor(bank × 0.2)`) rounds down to zero and so cannot hold today's teaser
back. Simulated over 20,000 consecutive days, the share of days where yesterday lands
on today's teaser — and the page therefore goes blank:

| Bank size | 1 | 2 | 3 | 4 | 5+ |
|---|---|---|---|---|---|
| Blank days | 100% | 50% | 16.5% | 8.4% | **0.00%** |

So in normal operation the guard never fires; it exists for a fresh install, where a
blank page is the correct answer.

#### Presentation

- Reached from a glossy blue link under the submission box (daily challenge only)
  and from the dropdown menu.
- **No submission box** — it is for reading, not replaying.
- Opens with a **spoiler warning** naming the date the puzzle ran, and both the
  question and the solution sit behind **separate tap-to-reveal veils**, so someone
  who missed the day can still attempt it before comparing. Blurred text is
  `user-select: none`, so it cannot be read by selecting through it.
- Shows the difficulty badge, support image, and hint alongside the answer and
  explanation.
- If there is no earlier challenge to publish, says so rather than erroring.

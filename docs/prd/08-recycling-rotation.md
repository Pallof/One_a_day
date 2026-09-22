# PRD 08 — The recycling rotation

**Status:** Spec of record · **Data:** `App_Data/rotation.json`

## In plain terms

One person writes every puzzle by hand, which creates two problems. If they get busy, the site
runs out — and it used to just show the same puzzle day after day, which kills a daily habit.
And because there's no archive, someone arriving in month six can never see months one to five.

**Recycling fixes both with one idea: when there's nothing new for today, an old puzzle comes
back around.** Think of it as a box of paper slips — each day one is drawn out and not put
back, so nothing repeats until the box is refilled.

## Problem

1. **The queue runs dry.** Before recycling, the daily page fell back to the most recent past
   teaser, so the *same* puzzle sat on the front page day after day. A returning solver sees a
   page they've already beaten and stops returning.
2. **Newcomers can't reach the back catalogue.** [PRD 04](04-archive-and-discovery.md) removed
   the archive on purpose, so good puzzles get written once and seen by whoever was around that
   week.

## The box

The bank of past teasers is a stack of slips in a box, and each day's challenge is **drawn out
and not put back.** Drawing without replacement is the whole point — independent random picks
would let the same teaser land twice in a week, which reads as a bug even when it isn't.

Three rules shape the draw:

| Rule | Value | Why |
|---|---|---|
| **Refill early** | when a fifth or fewer slips remain | Refilling on *empty* makes the last draws of a cycle forced — with two slips left, tomorrow's puzzle is one of two known teasers. Refilling early keeps every draw genuinely uncertain |
| **Cooldown** | the most recent fifth are held back | Because the box refills early, a teaser shown three days ago would otherwise be immediately eligible again |
| **Weighting** | chance falls the more often a teaser has been shown | Refilling early means roughly a fifth of the bank sits out each cycle. Without weighting, the same unlucky teasers draw the short straw cycle after cycle and go months unseen |

Refilling **replaces** the box rather than adding to it, so leftover slips are part of the
fresh set rather than duplicates sitting alongside it.

> **Why weights and not just re-rolling.** "Draw, and if this one's been shown a lot, draw
> again" reaches a similar result but can loop indefinitely, and its real odds are hard to
> state. A weighted draw is one pass, always finishes, and the odds are exactly the declared
> weights — which is what makes the behaviour testable at all.

## Requirements

### Precedence — new writing always wins

Resolving the challenge for a date follows three steps, in order:

1. **A teaser scheduled for that exact date runs on that date.** No draw happens, and the day
   is recorded as `new`. Fresh writing must never be undercut by the rotation.
2. **A day already settled keeps its teaser, permanently.**
3. **Otherwise, draw from the box.**

Only teasers **released on or before the date** are eligible. A future-dated teaser must never
be drawn early — that would front-run the author's own queue and burn a puzzle before its day.

### A settled day never changes

Once a date has been resolved, its teaser is written to history and fixed forever. Not an
optimisation — two things depend on it:

- A challenge must not change **under a solver mid-attempt.** Someone who opens the page at
  11pm and submits at 11:05 must be answering the same puzzle.
- **Yesterday's solution must not drift.** If yesterday could be re-drawn on each page load,
  the published solution would change between refreshes.

The corollary: resolving a day is a **write**, and any page load may trigger it. `ForDay` is
safe to call on every request and must stay that way.

### Deletions and additions

- A teaser **deleted** after the box was filled must not be drawable. Stale ids are filtered
  out at draw time rather than tracked eagerly.
- If a settled day points at a since-deleted teaser, that day **re-draws** rather than erroring.
- A teaser **added** mid-cycle joins at the next refill. The current cycle finishes on the set
  it started with.

### Yesterday's solution

The rotation is what makes `/yesterday` question-based rather than date-based, and the two must
resolve through the same path. Full rules — including the guard that hides the page when
yesterday and today land on the same teaser — live in [PRD 03](03-hints-and-solutions.md).

### Small banks

The cooldown is a floor, so a bank of fewer than five teasers holds nothing back. Accepted
rather than fixed: at that size there's genuinely too little material to avoid repeats, and the
visible consequence is confined to `/yesterday` going blank, which PRD 03 handles. A real bank
never gets close — the guard exists for a fresh install.

### Admin visibility

`/admin` shows the rotation as a panel so the author can see the mechanism working without
reading the file. The page exists only on the author's machine, so it reflects that machine's
copy unless the live file is copied down ([PRD 10](10-admin-authentication.md)):

- how many slips remain, out of what bank size, and the refill threshold
- how many **new** teasers are scheduled ahead — the real signal for whether the queue is dry
- the last 14 days: date, which teaser ran, and whether it was `new` or `recycled`
- a per-teaser **Shown** count with its current weight
- **Reset the box now**, which starts a fresh cycle over the whole bank

## Non-goals

- **Per-solver rotation.** Everyone sees the same puzzle on the same day — that's what
  "one a day" means, and it's what makes the challenge shareable. The rotation is a property of
  the calendar, not of the visitor.
- Tracking which teasers a given person has already seen
- Balancing difficulty across a cycle, or theming days
- Guaranteeing a teaser never repeats — recycling is the feature
- Reproducible or seeded draws in production

## Acceptance criteria

### The draw — `TeaserRotationTests`

- [x] The refill threshold is a fifth of the bank, and never zero
- [x] Draws are without replacement — no repeat within a cycle
- [x] The first draw fills the box from the whole bank and removes the slip taken
- [x] The box refills while slips remain, not on empty
- [x] The most recently shown teasers are held back by the cooldown
- [x] Deleted teasers are never drawn, even while stale ids sit in the box
- [x] Newly added teasers join at the next refill
- [x] An empty bank draws nothing rather than throwing; a bank of one still produces a draw
- [x] Draws spread across the bank rather than sticking on one teaser

### The weighting — `TeaserRotationTests`

- [x] A teaser's weight falls as it's shown more, and a neglected one is favoured
- [x] Weighting closes the gap over time rather than starving anyone permanently
- [x] Equal show counts leave the draw uniform
- [x] Draw frequencies match the declared weights over 40k draws
- [x] Supplying counts measurably changes the outcome versus not supplying them

### Spoiler safety — `YesterdaySolutionTests`

- [x] `/yesterday` never publishes the teaser running today
- [x] `/yesterday` shows nothing when the bank holds only today's teaser

### Scheduling — **implemented but not yet covered by tests**

> `Services/DailySchedule.cs` has no direct test coverage. These are the load-bearing
> invariants of the whole feature — the second in particular is what stops a challenge changing
> under a solver mid-attempt — and nothing would currently catch a regression. Worth closing.

- [ ] A teaser scheduled for a date wins over any draw on that date
- [ ] A day, once resolved, returns the same teaser on every later call
- [ ] Future-dated teasers are never drawn
- [ ] A settled day whose teaser was since deleted re-draws instead of erroring

## Implementation notes

Split three ways, deliberately:

| File | Holds | Why separate |
|---|---|---|
| `Models/TeaserRotation.cs` | The draw — thresholds, cooldown, weighting | Pure and static, no I/O and no clock. This is what makes the weighting testable over 100k draws |
| `Services/RotationStore.cs` | `rotation.json` — the box, day history, show counts | Persistence and locking only; no policy |
| `Services/DailySchedule.cs` | Precedence: scheduled → settled → draw | The one place that decides what runs on a day |

> **Operational note.** `rotation.json` is real state, not a cache. Losing it resets the box
> *and* the history, so already-published days become re-resolvable and could come back with a
> different teaser — retroactively changing what "yesterday's solution" was. Preserve it across
> deploys alongside `teasers.json` ([PRD 11](11-deployment.md)).

Production draws use an unseeded random number generator, so they aren't reproducible — correct
for the product. Tests call the draw directly with a seeded one, which is possible only because
the draw logic carries no state of its own.

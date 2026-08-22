# PRD 08 — The recycling rotation

**Status:** Spec of record · **Data:** `App_Data/rotation.json`

## Problem

One a Day publishes one teaser a day, and every teaser is written by hand by a single
author. That creates two failure modes that have nothing to do with the quality of the
puzzles:

1. **The queue runs dry.** The author gets busy for a fortnight. Before recycling, the
   daily page simply fell back to the most recent past teaser — so the *same* puzzle
   sat on the front page day after day. A daily habit cannot survive that; a returning
   solver sees a page they have already beaten and stops returning.
2. **Newcomers can't reach the back catalogue.** [PRD 04](04-archive-and-discovery.md)
   removed the archive on purpose, which means someone arriving in month six has no
   route to months one through five. Good puzzles get written once and seen by whoever
   happened to be around that week.

Recycling answers both with one mechanism: **once there is nothing new for today, an
old teaser comes back around.** The dry stretch stops being visible, and the back
catalogue reaches people who missed it the first time.

## The box

The model is a physical one, and the vocabulary in the code follows it: the bank of
past teasers is a stack of slips in a box, and each day's challenge is **drawn out and
not put back**.

Drawing without replacement is the whole point. Independent random picks would let the
same teaser land twice in a week, which reads as a bug even when it isn't. Emptying the
box before refilling guarantees a full cycle with no repeats.

Three rules shape the draw:

| Rule | Value | Why |
|---|---|---|
| **Refill early** | when `ceil(bank × 0.20)` or fewer slips remain | Refilling on *empty* makes the last few draws of a cycle forced — with two slips left, tomorrow's puzzle is one of two known teasers. Refilling with a fifth still in hand keeps every draw genuinely uncertain. |
| **Cooldown** | the most recent `floor(bank × 0.20)` teasers are held back | Because the box refills early, a teaser shown three days ago is immediately eligible again. The cooldown stops a puzzle reappearing on the heels of its last outing. |
| **Weighting** | chance ∝ `1 / (1 + times shown)` | Refilling early means roughly a fifth of the bank sits out each cycle. Without weighting, the same unlucky teasers can draw the short straw cycle after cycle and go months unseen. |

Refilling **replaces** the box rather than appending to it — the few leftover slips are
part of the fresh set, not duplicates sitting alongside it.

> **Why weights and not a reroll.** "Draw, and if the count is high, draw again" is
> rejection sampling. It reaches a similar distribution, but it can loop unboundedly
> and its true odds are hard to state precisely. A weighted draw is one pass, always
> terminates, and the odds are exactly the declared weights — which is what makes the
> behaviour testable at all.

## Requirements

### Precedence — new writing always wins

Resolving the challenge for a date follows three steps, in order:

1. **A teaser scheduled for that exact date runs on that date.** No draw happens, and
   the day is recorded as `new`. Writing fresh content must never be undercut by the
   rotation.
2. **A day already settled keeps its teaser, permanently.** See below.
3. **Otherwise, draw from the box.**

Only teasers **released on or before the date** are eligible. A future-dated teaser
must never be drawn early — that would front-run the author's own queue and burn a
puzzle before its day.

### A settled day never changes

Once a date has been resolved, that date's teaser is written to history and is fixed
forever. This is not an optimisation; two things depend on it:

- A challenge must not change **under a solver mid-attempt** — someone who opens the
  page at 11pm and submits at 11:05 must be answering the same puzzle.
- **Yesterday's solution must not drift.** If yesterday could be re-drawn on each page
  load, the published solution would change between refreshes.

The corollary is that resolving a day is a **write**, and any read path may trigger it.
`ForDay` is safe to call on every page load, and must stay that way.

### Deletions and additions

- A teaser **deleted** after the box was filled must not be drawable. Stale ids are
  filtered out of the box at draw time, not tracked eagerly.
- If a settled day points at a teaser that has since been deleted, that day **re-draws**
  a replacement rather than rendering an error.
- A teaser **added** mid-cycle joins the bank at the next refill. It does not jump into
  the box early — the current cycle finishes on the set it started with.

### Yesterday's solution

The rotation is what makes `/yesterday` question-based rather than date-based, and the
two must be resolved through the same path. The full rules — including the guard that
suppresses the page when yesterday and today land on the same teaser — live in
[PRD 03](03-hints-and-solutions.md).

### Small banks

The cooldown is a floor, so a bank of fewer than five teasers has a cooldown of zero
and cannot hold anything back. This is accepted rather than fixed: at that size there
is genuinely too little material to avoid repeats, and the visible consequence is
confined to `/yesterday` going blank, which PRD 03 handles. A real bank never gets
close — the guard exists for a fresh install.

### Admin visibility

`/admin` shows the rotation as a panel, so the author can see the mechanism working
without reading `rotation.json`:

- how many slips remain, out of what bank size, and the refill threshold
- how many **new** teasers are scheduled ahead — the real signal for whether the queue
  is dry
- the last 14 days: date, which teaser ran, and whether it was `new` or `recycled`
- a per-teaser **Shown** count with its current weight, in the teaser table
- **Reset the box now**, which empties the box so the next draw starts a fresh cycle
  over the whole bank

## Non-goals

- **Per-solver rotation.** Everyone sees the same puzzle on the same day — that is what
  "One a Day" means, and it is what makes the challenge shareable. The rotation is a
  property of the calendar, not of the visitor.
- Tracking which teasers a given person has already seen.
- Balancing difficulty across a cycle, or theming days.
- Guaranteeing a teaser never repeats — recycling is the feature.
- Reproducible or seeded draws in production.

## Acceptance criteria

### The draw — covered by `TeaserRotationTests`

- [x] The refill threshold is a fifth of the bank, and never zero
- [x] Draws are without replacement — no repeat within a cycle
- [x] The first draw fills the box from the whole bank and removes the slip taken
- [x] The box refills while slips remain, not on empty
- [x] The most recently shown teasers are held back by the cooldown
- [x] Deleted teasers are never drawn, even while stale ids sit in the box
- [x] Newly added teasers join at the next refill
- [x] An empty bank draws nothing rather than throwing
- [x] A bank of one still produces a draw
- [x] Draws spread across the bank rather than sticking on one teaser

### The weighting — covered by `TeaserRotationTests`

- [x] `WeightFor` falls as a teaser is shown more
- [x] A neglected teaser is favoured over well-worn ones
- [x] Weighting closes the gap over time rather than starving anyone permanently
- [x] Equal show counts leave the draw uniform
- [x] Draw frequencies match the declared weights over 40k draws
- [x] Supplying counts measurably changes the outcome versus not supplying them
- [x] Draws still work when no counts are supplied at all

### Spoiler safety — covered by `YesterdaySolutionTests`

- [x] `/yesterday` never publishes the teaser running today
- [x] `/yesterday` shows nothing when the bank holds only today's teaser

### Scheduling — **implemented but not yet covered by tests**

> `Services/DailySchedule.cs` has no direct test coverage. These are the load-bearing
> invariants of the whole feature — the second one in particular is what stops a
> challenge changing under a solver mid-attempt — and right now nothing would catch a
> regression in them. Worth closing.

- [ ] A teaser scheduled for a date wins over any draw on that date
- [ ] A day, once resolved, returns the same teaser on every later call
- [ ] Future-dated teasers are never drawn (`TeaserStore.ReleasedOn` is untested)
- [ ] A settled day whose teaser was since deleted re-draws instead of erroring

## Implementation notes

The feature is split three ways, deliberately:

| File | Holds | Why separate |
|---|---|---|
| `Models/TeaserRotation.cs` | The draw itself — thresholds, cooldown, weighting, `Draw` | Pure and static, no I/O and no clock. This is what makes the weighting testable over 100k draws. |
| `Services/RotationStore.cs` | `rotation.json` — the box, day history, show counts | Persistence and locking only; no policy. |
| `Services/DailySchedule.cs` | Precedence: scheduled → settled → draw | The one place that decides what runs on a day. |

`RotationState` persists three things: `Box` (ids still in the current cycle),
`History` (`{ Date, TeaserId, Recycled }` per resolved day), and `ShowCounts`.

> **Operational note.** `rotation.json` is real state, not a cache. Losing it resets the
> box *and* the history, which means already-published days become re-resolvable and
> could come back with a different teaser — retroactively changing what "yesterday's
> solution" was. It must be treated as data to preserve across deploys, alongside
> `teasers.json` — see [PRD 11](11-deployment.md).

The production RNG is an unseeded `Random` held by the `DailySchedule` singleton, so
draws are not reproducible — correct for the product. Tests call
`TeaserRotation.Draw` directly with a seeded `Random`, which is possible only because
the draw logic carries no state of its own.

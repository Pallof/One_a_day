# PRD 08 — The recycling rotation

**Status:** Spec of record · **Data:** `App_Data/rotation.json`

## In plain terms

One person writes every puzzle by hand, which creates two problems:

1. **The queue runs dry.** Before recycling, the daily page fell back to the latest past teaser, so
   the *same* puzzle sat on the front page day after day — and a solver who sees a page they've
   already beaten stops coming back.
2. **Newcomers can't reach the back catalogue.** With no archive ([PRD 04](04-archive-and-discovery.md)),
   a good puzzle is seen only by whoever was around that week.

**Recycling fixes both: when there's nothing new for today, an old puzzle comes back round.** Think
of a box of paper slips — each day one is drawn and not put back, so nothing repeats until the box
is refilled.

## The box

Drawing **without putting back** is the whole point: picking at random each day could land the same
teaser twice in a week, which reads as a bug even when it isn't.

| Rule | Value | Why |
|---|---|---|
| **Refill early** | when a fifth of the bank or fewer is left | Refilling only when *empty* makes a cycle's last draws forced — with two slips left, tomorrow is one of two known teasers |
| **Cooldown** | the most recently shown fifth are held back | Because the box refills early, a teaser shown three days ago would otherwise be straight back in |
| **Weighting** | the more a teaser has been shown, the lower its chance | About a fifth of the bank sits out each cycle; without weighting, the same unlucky teasers could miss out cycle after cycle and go months unseen |

Refilling **replaces** the box rather than topping it up, so leftover slips join the fresh set
instead of sitting beside it as duplicates.

> **Why weights, not re-rolling.** "Draw again if this one's been shown a lot" gets a similar result
> but can loop indefinitely, and its real odds are hard to state. A weighted draw takes one pass,
> always finishes, and its odds are exactly the stated weights — which is what makes it testable.

## Requirements

### New writing always wins

For each date, in order:

1. **A teaser scheduled for that date runs on that date** — no draw, recorded as `new`. The rotation
   must never undercut fresh writing.
2. **A day already settled keeps its teaser.**
3. **Otherwise, draw from the box** — only from teasers **dated on or before that day**. Drawing a
   future teaser early would jump the author's queue and use up a puzzle before its day.

### A settled day doesn't change

Once a date is worked out, its teaser is written to the history and kept, because:

- A challenge must not change **under a solver mid-attempt** — someone who opens the page at 11pm
  and submits at 11:05 must be answering the same puzzle.
- **Yesterday's solution must not drift** between page loads.

So working out a day is a **write** that any page load may trigger; `ForDay` must stay safe to call
on every visit.

**The one exception follows from step 1:** a teaser published *later* for an already-settled date
takes over that date on the site, while the history still records the earlier draw. The daily email
makes this visible — see the known gap in [PRD 15](15-email-subscriptions.md).

### Deletions and additions

- A teaser **deleted** after the box was filled is never drawn; stale entries are filtered out at
  draw time rather than tracked as they happen.
- A teaser **added** mid-cycle joins at the next refill; the current cycle finishes with the set it
  started with.

### Yesterday, small banks, admin

- `/yesterday` works yesterday out through this same schedule, which is what makes it question-based
  rather than date-based. Its rules, including hiding the page when yesterday and today land on the
  same teaser, are in [PRD 03](03-hints-and-solutions.md).
- The cooldown rounds down, so a bank of fewer than five holds nothing back. Accepted rather than
  fixed: at that size there's too little material to avoid repeats, and the only visible effect is
  `/yesterday` going blank. A real bank never gets that small; the guard is for a fresh install.
- Admin shows the box at work — on the author's machine, from that machine's copy of the file
  ([PRD 05](05-authoring-and-admin.md)).

## Non-goals

- **A different rotation per solver.** Everyone gets the same puzzle on the same day — that's what
  "one a day" means, and what makes the challenge shareable. The rotation belongs to the calendar,
  not the visitor.
- Tracking which teasers a person has seen; balancing difficulty across a cycle; themed days
- Guaranteeing a teaser never repeats — recycling is the feature
- Repeatable (seeded) draws on the live site

## Acceptance criteria

### The draw — `TeaserRotationTests`

- [x] The refill threshold is a fifth of the bank, never zero, and the box refills while slips
      remain, not on empty
- [x] The first draw fills the box from the whole bank and removes the slip taken; draws are without
      replacement — no repeat within a cycle
- [x] The most recently shown teasers are held back by the cooldown
- [x] Deleted teasers are never drawn, even while stale ids sit in the box; newly added teasers join
      at the next refill
- [x] An empty bank draws nothing rather than throwing; a bank of one still produces a draw
- [x] Draws spread across the bank rather than sticking on one teaser

### The weighting — `TeaserRotationTests`

- [x] A teaser's weight falls as it's shown more, a neglected one is favoured, and the gap closes
      over time rather than starving anyone permanently
- [x] Equal show counts leave the draw uniform; draw frequencies match the declared weights over 40k
      draws
- [x] Supplying counts measurably changes the outcome versus not supplying them

### Spoiler safety — `YesterdaySolutionTests`

- [x] `/yesterday` never publishes the teaser running today, and shows nothing when the bank holds
      only today's teaser

### Scheduling — not yet covered by tests

`Services/DailySchedule.cs` has no direct tests, though these are the feature's load-bearing rules —
the second is what stops a challenge changing under a solver — and nothing would catch a
regression. Worth closing.

- [ ] A teaser scheduled for a date wins over any draw on that date
- [ ] A day, once resolved, returns the same teaser on every later call
- [ ] Future-dated teasers are never drawn

## Implementation notes

| File | Holds | Why separate |
|---|---|---|
| `Models/TeaserRotation.cs` | The draw — refill point, cooldown, weighting | No files and no clock, so tests can run tens of thousands of draws |
| `Services/RotationStore.cs` | `rotation.json` — the box, day history, show counts | Saving and locking only; no rules |
| `Services/DailySchedule.cs` | The order: scheduled → settled → draw | The one place that decides what runs on a day |

> **`rotation.json` is real state, not a cache.** Losing it resets the box *and* the history, so days
> already published could come back with a different teaser — changing, after the fact, what
> "yesterday's solution" was. Keep it across deploys with `teasers.json` ([PRD 11](11-deployment.md)).

The live site draws with an unseeded random number generator, so draws can't be replayed — right for
the product. Tests call the draw directly with a seeded one, which works only because the draw keeps
no state of its own.

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

*Reworked 2026-11-04. The author: "whenever new questions are uploaded, they always take
precedent. And once they are done the box is reshuffled."*

For each date, in order:

1. **A day already settled keeps its teaser.** No exception, not for a new teaser and not for a pin.
2. **A teaser pinned to that date runs on it.** Recorded as `new`.
3. **Otherwise the next new teaser runs**: one that has never run on the site, earliest date first.
   Recorded as `new`. The rotation must never undercut fresh writing.
4. **Otherwise, draw from the box**: teasers that have run before, less any pinned to a later date.

#### What counts as new, and when it can start

- **New means never run on the site.** It comes from the day history in `rotation.json`, so there is
  nothing to mark by hand. The live site decides from its own copy; admin sees the author's copy,
  as current as the last `deploy/pull-live-data.sh`.
- **Never before the day after it arrives** *(author's decision)*. The site keeps the day it first
  had each teaser (`Arrived` in `rotation.json`). An upload restarts the site, so that is the upload
  day. A teaser uploaded today waits for tomorrow, even if nobody has opened the site yet today.
- A file written before 2026-11-04 has no `Arrived`. The first time it's filled, every teaser
  already in the bank counts as long arrived, so the ten questions dated 5 to 14 November 2026,
  uploaded on 4 November, ran on their dates. A missing `rotation.json` is treated the same way.

#### Dates set the order

- New teasers run **earliest date first, one a day**. A date is the order, and usually the day, but
  not a promise: a gap in the dates, a date already gone or one far ahead just closes up, so a gap
  never falls back to a repeat while a new teaser is waiting *(author's decision)*.
- Admin defaults a new teaser's date to after the last one waiting, so its date is normally the day
  it runs, and lists the day each will run under **Up next** ([PRD 05](05-authoring-and-admin.md)).
- Two teasers on one date (admin refuses it, a hand edit could do it) run in their order in
  `teasers.json`.

#### Pinning a teaser to a day

For a holiday question, say *(author's decision)*. **Pinned** in `teasers.json`, the **Pin to this
date** tick in admin, off by default.

- A pinned teaser runs on **exactly its date**, ahead of any new teaser waiting; the queue picks up
  the day after.
- It **never runs before its date**: it stays out of the queue, and out of the box, until then.
- One that arrives on its day or later has missed its pin: the live site settles today long before
  an upload lands. It runs next, being the earliest date waiting.
- Afterwards it recycles like any other. One that has run before can be pinned again; running again
  on its day doesn't reshuffle the box.

#### The reshuffle

- **The first recycled day after new teasers have run starts a fresh box**: every teaser that has
  run, the new ones just shown included *(author's decision)*. The cooldown and weights apply as
  ever.
- So the newest fifth of the bank sits out that first draw under the cooldown, but the first of a
  long run of new teasers can come back soon after: of the ten that ran 5 to 14 November 2026, the
  first three were drawable again from 15 November. Shown only once, they also weigh as much as the
  older teasers or more. Accepted: the author preferred every teaser in the fresh box to holding the
  new ones back until the next refill.
- It's worked out from the history rather than stored: *has any teaser run for the first time since
  the last recycled day?* So it holds even for a run of new teasers that started before this change
  was deployed.

### A settled day doesn't change

Once a date is worked out, its teaser is written to the history and kept, because:

- A challenge must not change **under a solver mid-attempt** — someone who opens the page at 11pm
  and submits at 11:05 must be answering the same puzzle.
- **Yesterday's solution must not drift** between page loads.

So working out a day is a **write** that any page load may trigger; `ForDay` must stay safe to call
on every visit. Two page loads can work out the same day at once: whichever records it first wins,
and the other gets that teaser back.

> *Until 2026-11-04 there was one exception: a teaser dated today took over the day even if it was
> already settled, so an upload mid-day switched the live question after the 7am email had gone out
> with the old one ([PRD 15](15-email-subscriptions.md)). That day the author moved ten new questions
> from starting that day to starting the next, by hand, to avoid it. Step 1 now has no exception.*

### Deletions and additions

- A teaser **deleted** after the box was filled is never drawn; stale entries are filtered out at
  draw time rather than tracked as they happen.
- A teaser **added** runs as new first, and goes into the box at the reshuffle after the new
  teasers have run.

### Yesterday, small banks, admin

- `/yesterday` works yesterday out through this same schedule, which is what makes it question-based
  rather than date-based. Its rules, including hiding the page when yesterday and today land on the
  same teaser, are in [PRD 03](03-hints-and-solutions.md).
- The cooldown rounds down, so a bank of fewer than five holds nothing back. Accepted rather than
  fixed: at that size there's too little material to avoid repeats, and the only visible effect is
  `/yesterday` going blank. A real bank never gets that small; the guard is for a fresh install.
- **The cooldown can never hold back everything left in the box.** The box refills while more than
  a fifth of the bank remains, and at most a fifth, rounded down, is held back — so a slip is always
  drawable. A "hold nothing back after all" fallback for that case could never run and was removed
  in the test audit of 2026-09-28. Raising the cooldown fraction above the refill fraction would
  break this; a test checks it at every bank size up to 100.
- Admin shows the box at work, how many new teasers are waiting and the day each will run, on the
  author's machine, from that machine's copy of the file ([PRD 05](05-authoring-and-admin.md)).

## Non-goals

- **A different rotation per solver.** Everyone gets the same puzzle on the same day — that's what
  "one a day" means, and what makes the challenge shareable. The rotation belongs to the calendar,
  not the visitor.
- Tracking which teasers a person has seen; balancing difficulty across a cycle; themed days
  (pinning one teaser to a date is supported, a rule like "wordplay on Mondays" isn't)
- Guaranteeing a teaser never repeats — recycling is the feature
- Repeatable (seeded) draws on the live site

## Acceptance criteria

### The draw — `TeaserRotationTests`

- [x] The refill threshold is a fifth of the bank rounded up (11 teasers give 3), never zero, and the
      box refills while slips remain, not on empty
- [x] The first draw fills the box from the whole bank and removes the slip taken; draws are without
      replacement — no repeat within a cycle
- [x] The most recently shown teasers are held back by the cooldown
- [x] Deleted teasers are never drawn, even while stale ids sit in the box; newly added teasers join
      at the next refill
- [x] An empty bank draws nothing rather than throwing; at every bank size up to 100, a box at its
      tightest — just above the refill line, every slip recently shown — still produces a draw
- [x] Draws spread across the bank rather than sticking on one teaser

### The weighting — `TeaserRotationTests`

- [x] A teaser's weight falls as it's shown more, a neglected one is favoured, and the gap closes
      over time rather than starving anyone permanently — narrower than drawing blind leaves it,
      from the same seed
- [x] Equal show counts leave the draw uniform; draw frequencies match the declared weights over 40k
      draws
- [x] Supplying counts measurably changes the outcome versus not supplying them

### Spoiler safety — `YesterdaySolutionTests`

- [x] `/yesterday` never publishes the teaser running today, and shows nothing when the bank holds
      only today's teaser

### Scheduling — `DailyScheduleTests`

Added 2026-11-04 with the rework above; until then `DailySchedule` had no direct tests. Each was
shown to fail with a deliberate break, then restored.

- [x] A settled day keeps its teaser when one dated or pinned to that day arrives later, across the
      restart an upload causes; two requests settling one day get the same teaser
- [x] A new teaser waits for the day after it arrives, even while today is unsettled; the day it
      arrived is kept across a restart; teasers in the bank before arrivals were kept can run at once
- [x] New teasers run before any repeat, earliest date first, and a gap, a past date and a date far
      ahead all close up
- [x] A pinned teaser takes its day and the queue picks up after; it never runs before its day, nor
      is drawn from the box before it; one that arrives on its day runs the next day
- [x] Once new teasers have run, the box is reshuffled with every teaser, the new ones included, and
      only once; a pinned teaser running again doesn't reshuffle it
- [x] Admin's **Up next** puts each teaser on the day the site then runs it, and settles nothing

## Implementation notes

| File | Holds | Why separate |
|---|---|---|
| `Models/TeaserRotation.cs` | The draw — refill point, cooldown, weighting | No files and no clock, so tests can run tens of thousands of draws |
| `Services/RotationStore.cs` | `rotation.json` — the box, day history, show counts, the day each teaser arrived | Saving and locking only; no rules |
| `Services/DailySchedule.cs` | The order: settled → pinned → new → draw, and admin's Up next | The one place that decides what runs on a day |

> **`rotation.json` is real state, not a cache.** Losing it resets the box *and* the history, so days
> already published could come back with a different teaser — changing, after the fact, what
> "yesterday's solution" was. Every teaser would also look new again, and the whole bank would run
> through in date order before the box opened. Keep it across deploys with `teasers.json`
> ([PRD 11](11-deployment.md)), and never upload the author's copy over the live one.

The live site draws with an unseeded random number generator, so draws can't be replayed — right for
the product. Tests call the draw directly with a seeded one, which works only because the draw keeps
no state of its own.

# PRD 01 — Daily challenge

**Status:** Spec of record · **Route:** `/`

## In plain terms

The home page shows one puzzle a day. Days change at **midnight California time** no matter
where the server is, so everyone gets the same puzzle on the same day. Solve it and you get
confetti, the box locks, and a countdown says when the next one arrives — and the page reloads
itself at midnight, so a tab left open overnight isn't showing yesterday's.

One thing it deliberately never does: show the puzzle's own date. Because old puzzles get
recycled, printing the date would announce that today's is a repeat.

## Problem

A daily puzzle only builds a habit if "today's puzzle" is unambiguous and the same
for everyone, and if finishing it leaves the solver with a reason to return.

## Requirements

### Selecting today's teaser

- The home page must show the teaser scheduled for **today in Pacific Time**.
- If today has no scheduled teaser, one is **recycled** from the bank — see
  [PRD 08](08-recycling-rotation.md), which owns the selection rules.
- If no teaser exists at all, show a friendly "check back soon" message.
- Teasers dated in the future must never appear here.

> **Superseded, and worth knowing why.** This used to read: *fall back to the most
> recent past teaser, and label it "From Friday 3 July — today's teaser hasn't been
> posted yet."* Both halves are now wrong.
>
> The fallback left the **same puzzle on the front page for days** during a dry
> spell, which is what the recycling box exists to prevent. And the dated label
> would now be actively harmful: a recycled teaser was written months ago, so
> printing its date announces that the puzzle is a repeat. There is deliberately no
> date note on a teaser anywhere on this page.

### Today's date

- A **dateline** sits above the masthead showing the current day (*"Tuesday,
  September 1"*).
- It must come from `AppTime.Today` — **today's date, never the teaser's**. The
  distinction is the whole point: with recycling those two routinely differ, and
  rendering the teaser's date would leak that it is a repeat.

### Day boundary

- Days roll over at **midnight `America/Los_Angeles`**, regardless of where the
  server runs, and the boundary must be DST-correct (PDT/PST).
- Every surface that reasons about "today" — home, admin
  defaults, the countdown — must share this one clock. A single source
  (`AppTime`) is required; per-page `DateTime.Now` is a defect.

### Submission

- One multi-line answer box and a **Submit answer** button.
- Submit is disabled while the box is empty.
- Input is capped at **300 characters**, enforced in the browser *and* server-side
  (a crafted request must not bypass it).
- Evaluation rules: see [PRD 02](02-answer-evaluation.md).

### Feedback

- **Correct** → `Correct! Solved in N attempt(s). 🎉`, the box and button lock, and
  the worked solution appears if the author wrote one.
- **Incorrect** → an encouraging retry message; the solver may try again without limit.
- A correct answer must trigger a **celebration animation** (confetti).
  - It must respect `prefers-reduced-motion` and not render for solvers who have
    asked their OS to reduce motion.
  - It must be self-contained (no external library or CDN).

### Post-solve state

After solving today's challenge, show:
- The message *"Please come back soon for when the next challenge arrives!"*
- A **live countdown** (per-second) to midnight Pacific.
- A note naming the timezone.
- When the countdown reaches zero, the page must **reload itself** and serve the
  new challenge, so a tab left open overnight is correct.

The countdown belongs to the daily challenge, which since [PRD 04](04-archive-and-discovery.md)
is the only place a teaser is playable.

### Statistics

Community stats are hidden until the solver has solved the puzzle themselves —
see [PRD 06](06-community-feedback.md#statistics).

## Non-goals

- Any notion of "missing" a day (there is no streak penalty; see [PRD 12](12-streaks-and-sharing.md))

## Acceptance criteria

- [x] Home shows today's teaser; a day with none scheduled recycles one
      ([PRD 08](08-recycling-rotation.md))
- [x] **No date is ever shown for the teaser itself** — only today's dateline
- [x] Future teasers never render on `/`
- [x] Countdown ticks each second and auto-reloads at midnight PT
- [x] Confetti fires on a correct answer and is suppressed under reduced-motion
- [x] Submission length enforced on both client and server

## Implementation notes

`Components/Pages/Home.razor` selects the teaser via **`DailySchedule.ForDay`**,
which applies the recycling rules in [PRD 08](08-recycling-rotation.md). All
challenge UI lives in `Components/ChallengeView.razor`; since [PRD 04](04-archive-and-discovery.md)
removed the archive, **Home is its only caller**. `Services/AppTime.cs` owns the
clock. `wwwroot/js/confetti.js` is a dependency-free canvas animation invoked by JS
interop.

> **Dead code, deliberately left in place.** `TeaserStore.GetCurrent` and
> `GetPreviousBefore` implemented the old date-based selection. Nothing in the app
> calls either any more — `DailySchedule` replaced both — but they still have test
> coverage, so they are alive in `OneADay.Tests` and dead in production. If either
> is ever reached for again, check it against PRD 08 first: they do **not** respect
> the rotation, and using one is how the yesterday page regressed before.

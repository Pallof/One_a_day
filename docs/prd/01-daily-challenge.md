# PRD 01 — Daily challenge

**Status:** Spec of record · **Route:** `/`

## In plain terms

One puzzle a day, on the home page. A daily puzzle only builds a habit if "today's puzzle" is the
same for everyone and finishing it gives a reason to come back. So the day changes at **midnight
California time** wherever the server is; solving brings confetti and a countdown to the next
one; and the page reloads itself at midnight, so a tab left open overnight isn't stale.

It never shows the puzzle's own date: puzzles get recycled, and the date would give away a repeat.

## Requirements

### Which teaser

- The teaser scheduled for **today (Pacific)**; if there isn't one, a **recycled** one
  ([PRD 08](08-recycling-rotation.md)); if there are no teasers at all, a friendly "check back
  soon".
- Never a teaser dated in the future.

> **History:** this once fell back to the latest past teaser, labelled *"From Friday 3 July —
> today's teaser hasn't been posted yet."* The fallback left the **same puzzle up for days** in a
> dry spell, which recycling now prevents, and the label would now reveal that a recycled puzzle
> is a repeat.

### Dates and the clock

- A **dateline** above the masthead shows **today's** date (*"Tuesday, September 1"*), never the
  teaser's — with recycling the two routinely differ.
- Days roll over at **midnight `America/Los_Angeles`**, correct through daylight saving, wherever
  the server runs.
- Everything that asks "what day is it?" — home, admin's date defaults, the countdown — uses one
  clock, `AppTime`. A page reading the computer's own clock is a bug.

### Answering

- One answer box; **Submit answer** is disabled while it's empty.
- Answers are capped at **300 characters**, in the browser *and* on the server, so a crafted
  request can't get round it. How answers are judged: [PRD 02](02-answer-evaluation.md).
- **Wrong** → an encouraging message; unlimited retries.
- **Right** → *"Correct! Solved in N attempts. 🎉"*, the box locks, and the author's explanation
  shows if there is one. **Confetti** fires — self-contained (no outside library), and skipped for
  anyone whose device asks for reduced motion.

### After solving

- A **dialog** announces the solve, with the countdown, the explanation and the statistics.
  *History: the countdown used to sit only below the answer box, off-screen on most displays.*
- The page keeps *"Please come back soon for when the next challenge arrives!"*, a countdown
  **ticking each second** to midnight Pacific, and a note naming the time zone — so closing the
  dialog loses nothing.
- At zero the page **reloads itself** with the new challenge.
- Statistics appear only after solving ([PRD 06](06-community-feedback.md#statistics)).

## Non-goals

- Any notion of "missing" a day — no streak penalty ([PRD 12](12-streaks-and-sharing.md))

## Acceptance criteria

- [x] Home shows today's teaser; a day with none scheduled recycles one
      ([PRD 08](08-recycling-rotation.md))
- [x] **No date is ever shown for the teaser itself** — only today's dateline
- [x] Future teasers never render on `/`
- [x] Countdown ticks each second and auto-reloads at midnight PT
- [x] Confetti fires on a correct answer and is suppressed under reduced-motion
- [x] Submission length enforced on both client and server

## Implementation notes

`Components/Pages/Home.razor` picks the teaser with `DailySchedule.ForDay` (the PRD 08 rules).
`Components/ChallengeView.razor` holds the challenge itself; since the archive went
([PRD 04](04-archive-and-discovery.md)), Home is its only user. `Services/AppTime.cs` is the
clock; `wwwroot/js/confetti.js` the confetti.

> **Dead code, kept on purpose.** `TeaserStore.GetCurrent` and `GetPreviousBefore` did the old
> date-based selection. Nothing calls them now, but tests still cover them. They ignore the
> rotation — using one is how the yesterday page regressed before
> ([PRD 03](03-hints-and-solutions.md)) — so check PRD 08 before reaching for either.

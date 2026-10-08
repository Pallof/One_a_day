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

### Scratch pad

Paper for working a puzzle out, for anyone without pen and paper to hand *(author's decisions,
2026-10-07)*.

- **On this page only**, behind a round floating button, bottom left — a little whiteboard with a
  pencil on it. Bottom left mirrors *Report an issue* at bottom right and stays clear of the
  Twenty Four nudge, which comes in from the right ([PRD 09](09-visual-design.md)).
- It opens above its button, **with no backdrop**, so the question stays in view.
- **Pen, Eraser, Undo, Clear — and nothing else.** No colours, shapes or fill: *"keep it
  simple."* The eraser removes ink rather than painting white; Undo takes back the last stroke,
  eraser strokes included.
- **Clear wipes the whole board at once** *(added 2026-10-08: rubbing out every bit is
  tedious)*. **No "are you sure?"** *(author's call)* — instead, Undo straight after a Clear
  brings the board back, so a slip costs nothing. A Clear on an empty board, or a second Clear
  in a row, does nothing, so one Undo always undoes it. The four tools share the toolbar row
  equally, so it fits a 320px phone.
- **The eraser shows its reach as a ring** exactly as wide as what it wipes — one number in the
  script sizes both. Picking the eraser puts the ring mid-pad straight away. With a mouse it is
  the cursor; on a phone, where a finger hides whatever is under it, it stays where the finger
  lifted.
- **Closing it**: the ✕ on the pad, or the whiteboard button again. Tapping elsewhere does *not*
  close it — while working, you'll tap the answer box and scroll the question. Closing only hides
  the drawing; it's there when the pad opens again.
- **All in the browser.** No stroke goes to the server — each would be a round trip, laggy on a
  phone — and **nothing is saved**: leaving or reloading the page wipes it, like scrap paper. So
  it costs the server nothing.
- A finger on the pad draws instead of scrolling the page; the pad is at most 240px tall (40% of
  the screen held sideways), leaving room to scroll past it.
- A narrower pad — a phone turned — shows less of the drawing rather than shrinking it; strokes
  outside the edge come back when it widens.

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
- [x] **Scratch pad** — `ScratchPadTests`, each mutation-verified: on the challenge page, and not
      in the layout or on Twenty Four; Pen, Eraser, Undo, Clear and nothing else, with a drawing area;
      starts closed with the pen picked; nothing on it has a server-side handler
- [x] Scratch pad, by hand in a 375px phone view: strokes draw and the page doesn't scroll; the
      eraser cuts a line; Undo brings it back, then removes the stroke before; closing and
      reopening keeps the drawing; idle, it stays open; at 320px it fits with no sideways scroll
- [x] Clear, by hand at 375px: two strokes, then Clear empties the board; a second Clear does
      nothing; one Undo brings both strokes back, a second takes one away. At 320px the four
      tools are 49px each and nothing is cut off
- [x] Eraser ring, by hand: picking the eraser shows a 20px ring mid-pad; one tap on a line cuts
      a 19.5px gap (the rest is edge smoothing) with the ring centred on it. The ring's place
      beside the drawing area is pinned by `ScratchPadTests` (mutation-verified)

## Implementation notes

`Components/Pages/Home.razor` picks the teaser with `DailySchedule.ForDay` (the PRD 08 rules).
`Components/ChallengeView.razor` holds the challenge itself; since the archive went
([PRD 04](04-archive-and-discovery.md)), Home is its only user. `Services/AppTime.cs` is the
clock; `wwwroot/js/confetti.js` the confetti. The scratch pad is `Components/ScratchPad.razor`
(markup only, no Blazor handlers) and `wwwroot/js/scratchpad.js` (everything it does).

> **Removed 2026-09-28:** `TeaserStore.GetCurrent` and `GetPreviousBefore`, which did the old
> date-based selection. Nothing called them, and their tests guarded nothing the site used. They
> also ignored the rotation — using one is how the yesterday page regressed before
> ([PRD 03](03-hints-and-solutions.md)) — so a day's teaser, or the day before's, comes only from
> `DailySchedule.ForDay` (PRD 08).

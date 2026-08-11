# PRD 01 — Daily challenge

**Status:** Spec of record · **Route:** `/`

## Problem

A daily puzzle only builds a habit if "today's puzzle" is unambiguous and the same
for everyone, and if finishing it leaves the solver with a reason to return.

## Requirements

### Selecting today's teaser

- The home page must show the teaser scheduled for **today in Pacific Time**.
- If today has no teaser, it must **fall back to the most recent past teaser** and
  label it plainly (*"From Friday 3 July 2026 — today's teaser hasn't been posted
  yet."*) rather than showing an empty page.
- If no teaser exists at all, show a friendly "check back soon" message.
- Teasers dated in the future must never appear here.

### Day boundary

- Days roll over at **midnight `America/Los_Angeles`**, regardless of where the
  server runs, and the boundary must be DST-correct (PDT/PST).
- Every surface that reasons about "today" — home, archive, question pages, admin
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

The countdown belongs to the daily challenge only — it must not appear on archived
question pages, where there is nothing to wait for.

### Statistics

Community stats are hidden until the solver has solved the puzzle themselves —
see [PRD 06](06-community-feedback.md#statistics).

## Non-goals

- Any notion of "missing" a day (there is no streak penalty; see [PRD 12](12-streaks-and-sharing.md))

## Acceptance criteria

- [x] Home shows today's teaser; a missing day falls back with a dated note
- [x] Future teasers never render on `/`
- [x] Countdown ticks each second and auto-reloads at midnight PT
- [x] Confetti fires on a correct answer and is suppressed under reduced-motion
- [x] Submission length enforced on both client and server

## Implementation notes

`Components/Pages/Home.razor` selects the teaser via `TeaserStore.GetCurrent`;
all challenge UI lives in the shared `Components/ChallengeView.razor` (also used by
archived question pages, so the two cannot drift). `Services/AppTime.cs` owns the
clock. `wwwroot/js/confetti.js` is a dependency-free canvas animation invoked by JS
interop.

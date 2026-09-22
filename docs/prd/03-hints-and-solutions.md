# PRD 03 — Hints & solutions

**Status:** Spec of record

## In plain terms

Help has to exist — a puzzle with no way forward is a dead end — but help one click away turns
the puzzle into reading. So help is earned. A hint unlocks after your first attempt. Wrong tries
never unlock the solution, however many you make: you see it by **solving** the puzzle, or the next
day on the "yesterday's solution" page. **Solving or waiting earns the answer; trying harder
doesn't.**

The subtle part: old puzzles get recycled as today's challenge, so *"is this puzzle old?"* is not
the same question as *"is this puzzle live?"* Mixing them up would give away the answer people are
working on right now.

## Requirements

### Hints

- Optional per teaser; none written, none shown. It sits **between the question and the answer
  box**, hidden by default.
- It **unlocks after the first attempt.** Before that, a locked notice that clearly isn't a button:
  *"🔒 Give it a try first — a hint unlocks after your first attempt."*
- One click shows it; **clicking it again hides it** ("Click to hide").
- Using a hint costs nothing and isn't recorded.

### Solutions

- Optional per teaser. **On a correct answer** the explanation shows at once — the puzzle is over.
  None written, no empty box.
- **Solutions unlock the day after a challenge runs — never while it's live.** No amount of effort
  opens the answer early, so there is deliberately **no attempt threshold**.
- A **"Reveal solution"** button appears only when the teaser is **not the one running now** and
  the solver hasn't solved it. "Not running now" is *not* "dated in the past": a recycled teaser
  is dated in the past while being today's challenge. Decide from the schedule, never the
  teaser's date.
- Revealing shows the main accepted answer and the explanation, and locks the answer box.
- On a **live** challenge, after the first attempt, a note sets expectations — *"🔒 No peeking
  today — the solution unlocks tomorrow."* — so nobody hunts for a button that isn't there. On a
  past one, the retry message points to the button: *"Not quite — try again, or reveal the
  solution."*

Since the archive went ([PRD 04](04-archive-and-discovery.md)), today's is the only challenge page,
so no page offers a reveal; the past-challenge rules apply only if such a page returns. Scheduled
teasers can't be reached at all, and must never offer a reveal if they could.

## Non-goals

- Several hints per puzzle, revealed in turn
- Scoring or penalising hints or reveals
- Remembering hint state across page loads

## Acceptance criteria

- [x] Hint locked before the first attempt, unlocked after it
- [x] Revealed hint re-conceals on click
- [x] Reveal button available immediately on a past challenge, with no attempt threshold
- [x] Reveal button **never** appears on a live challenge, however many attempts are made
      (verified with 4 failed attempts on a teaser dated today)
- [x] Live challenge shows the "unlocks tomorrow" note after the first attempt
- [x] Scheduled (future) teasers never reveal
- [x] Solved teasers with no written solution render no empty panel

*All seven were ticked before the archive was removed, while past teasers still had pages. No
automated test covers the reveal button.*

## Yesterday's solution (`/yesterday`)

With no archive, this is the one place a solution is published.

**It publishes whichever teaser actually ran yesterday — not the one dated yesterday.** With
recycling ([PRD 08](08-recycling-rotation.md)), a puzzle written in March can be the challenge in
August, so its date says nothing about when it last ran. The page works yesterday out the same
way as any other day:

1. Work out today's challenge, then yesterday's. Doing today first means the result doesn't depend
   on which page a visitor opens first, and it puts today's teaser in the rotation's cooldown, so
   yesterday can't draw it.
2. Publish what came back for yesterday.

> **History — regression:** an earlier version fell back to a *date-based* lookup whenever the
> rotation's history was thin — every fresh deploy and any gap in visits. It kept returning the
> second-newest teaser the author had typed in, so the page barely changed between uploads — the
> opposite of what recycling is for. Don't reintroduce a date fallback.

### Never publish the live answer

If yesterday comes back as **the same teaser as today**, the page shows nothing — the solution
would give away the live challenge.

That can only happen with **fewer than five** teasers, where the cooldown (a fifth of the bank,
rounded down) is zero. Simulated over 20,000 days, the share of days the page goes blank:

| Teasers in the bank | 1 | 2 | 3 | 4 | 5+ |
|---|---|---|---|---|---|
| Blank days | 100% | 50% | 16.5% | 8.4% | **0.00%** |

So in normal use the guard never fires. It exists for a fresh install, where a blank page is the
right answer.

### Presentation

- Linked from under the answer box on the daily challenge, and from the main menu.
- **No answer box** — it's for reading, not replaying.
- A **spoiler warning** names the date the puzzle ran. The question and the solution each sit
  behind their **own tap-to-reveal blur**, so someone who missed the day can try it before
  comparing. Blurred text can't be selected, so it can't be read by highlighting it.
- Shows the difficulty, picture and hint alongside the answer and explanation.
- With nothing earlier to publish, it says so rather than erroring.

## Implementation notes

In `Components/ChallengeView.razor` the hint gate is `MinAttemptsBeforeHint = 1`, and the reveal
depends only on the `CanReveal` parameter — on what's live, not on effort. The home page passes
`CanReveal="false"` unconditionally, which is also what keeps recycling safe; any future caller
must decide from the schedule, never from `Teaser.Date`. `/yesterday` uses
`DailySchedule.PreviousBefore`.

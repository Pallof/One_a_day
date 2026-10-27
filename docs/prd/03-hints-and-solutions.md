# PRD 03 — Hints & solutions

**Status:** Spec of record

## In plain terms

The site shows no hints *(author's decision, 2026-10-27)*. Wrong tries never unlock the solution,
however many you make: you see it by **solving** the puzzle, or the next day on the "yesterday's
solution" page. **Solving or waiting earns the answer; trying harder doesn't.**

The subtle part: old puzzles get recycled as today's challenge, so *"is this puzzle old?"* is not
the same question as *"is this puzzle live?"* Mixing them up would give away the answer people are
working on right now.

## Requirements

### Hints

- **Not shown anywhere on the site** *(author's decision, 2026-10-27)*: not on today's challenge,
  not on yesterday's page. A review that day found most of them either gave the trick away or
  were too vague to help.
- **The bank keeps them.** Each teaser still has its `Hint` field in `teasers.json`, and the admin
  form still takes one, so bringing hints back is a change to the pages and not to the data.
- No email ever carried one ([PRD 15](15-email-subscriptions.md)).

*History: until 2026-10-27 the hint sat between the question and the answer box. It unlocked
after the first attempt, behind a locked notice before that; one click showed it and another hid
it. On yesterday's page it sat behind its own tap-to-reveal blur. Using one cost nothing and
wasn't recorded.*

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
- On a **live** challenge, after the first attempt, a note sets expectations, *"No peeking
  today. The solution unlocks tomorrow."*, so nobody hunts for a button that isn't there. On a
  past one, the retry message points to the button: *"Not quite. Try again, or reveal the
  solution."*

Since the archive went ([PRD 04](04-archive-and-discovery.md)), today's is the only challenge page,
so no page offers a reveal; the past-challenge rules apply only if such a page returns. Scheduled
teasers can't be reached at all, and must never offer a reveal if they could.

## Non-goals

- Several hints per puzzle, revealed in turn
- Scoring or penalising hints or reveals
- Remembering hint state across page loads

## Acceptance criteria

- [x] **No hint on today's challenge or yesterday's page**, though the teaser has one: not before
      an attempt, not after wrong ones, not once the solution shows (`NoHintTests` and
      `YesterdaySolutionTests`, mutation-verified 2026-10-27 by putting a hint back on each page)
- [x] Reveal button available immediately on a past challenge, with no attempt threshold
- [x] Reveal button **never** appears on a live challenge, however many attempts are made
      (verified with 4 failed attempts on a teaser dated today)
- [x] Live challenge shows the "unlocks tomorrow" note after the first attempt
- [x] Scheduled (future) teasers never reveal
- [x] Solved teasers with no written solution render no empty panel

*The five after the first were ticked before the archive was removed, while past teasers still
had pages. `NoHintTests` clicks the reveal button and checks the solution shows; nothing
automated checks it stays away from a live challenge. Two hint criteria (locked until the first
attempt; hidden again on a second click) were dropped with the hints on 2026-10-27.*

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
- A **spoiler warning** names the date the puzzle ran, and says the answer is hidden below.
- **The question is shown; the solution sits behind a tap-to-reveal blur** *(author's decision,
  2026-10-14)*. The question gives nothing away and is what someone who missed the day needs to
  have a go. Blurred text can't be selected, so it can't be read by highlighting it. No hint
  *(2026-10-27, see [Hints](#hints))*.
  *History: until 2026-10-14 the question was the one blurred, and the hint showed with it; from
  then until 2026-10-27 the hint had a blur of its own.*
- Shows the difficulty and picture with the question, and the answer with its explanation.
- With nothing earlier to publish, it says so rather than erroring.
- **Nothing on it can be copied**, revealed or not *(author's decision, 2026-10-18)*: see
  [PRD 01](01-daily-challenge.md#the-puzzle-cant-be-copied).

## Implementation notes

In `Components/ChallengeView.razor` the reveal depends only on the `CanReveal` parameter: on
what's live, not on effort. The home page passes
`CanReveal="false"` unconditionally, which is also what keeps recycling safe; any future caller
must decide from the schedule, never from `Teaser.Date`. `/yesterday` uses
`DailySchedule.PreviousBefore`.

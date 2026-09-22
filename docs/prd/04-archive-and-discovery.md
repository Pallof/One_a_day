# PRD 04 — No archive (one a day means one a day)

**Status:** Spec of record · **Routes:** `/twentyfour` (and legacy `/questions`)
· **Supersedes:** the Archive & discovery spec, removed 2026-08-17

## In plain terms

**There is no way to browse old puzzles, and that's deliberate rather than missing.** An
archive let a visitor burn through the entire bank in one sitting and then have no reason to
come back tomorrow — which defeats the whole point of a daily puzzle. It was removed rather
than tuned, because the feature itself was at odds with the goal.

Old puzzle addresses now return "not found" rather than being merely unlinked, because
unlinking leaves the whole bank one guessed address away. The slot it used to occupy holds the
24 game instead, which invents its own puzzles and so can be played all afternoon without using
any up.

## Problem

The product's whole premise is **one puzzle a day**: the daily rhythm is what makes
it a habit and what makes a single teaser feel worth thinking about. An archive of
every past and scheduled teaser quietly worked against that. A visitor could open
the list and burn through the entire bank in a single sitting — and once they had,
there was nothing to come back for tomorrow.

That is not a bug in how the archive was built; the archive was working exactly as
specified. The feature itself was at odds with the goal, so it was removed rather
than tuned.

A second problem compounded it: scheduled teasers were browsable as a "sneak peek",
so tomorrow's challenge was never really a surprise.

## Decision

**There is no way to browse or replay past and future teasers.** The only puzzle a
solver can reach is the one live today.

## Requirements

1. Neither an index of teasers nor per-teaser pages may exist.
2. Past and future teasers must be **unreachable by URL**, not merely unlinked.
   Removing navigation while leaving `/questions/{date}` routable would leave the
   whole bank one guessed URL away and defeat the point. These paths must return
   **404**.
3. Nothing in the UI may advertise browsing past questions — including prose on
   the About page.
4. Old links and bookmarks should land somewhere sensible rather than a dead end,
   so `/questions` is kept and serves the placeholder page below.
5. The daily challenge at `/` is unaffected: a day with nothing scheduled still gets
   a puzzle, now by recycling one ([PRD 08](08-recycling-rotation.md)) rather than by
   the date-based fallback this document originally described.
6. **Teaser data is untouched.** Every teaser stays in `teasers.json` and remains
   visible in admin — only the public browsing surface is gone, so a curated
   archive can be reintroduced later without re-authoring anything.

### The Twenty Four game

The slot vacated by the archive now holds a small game, specified in
[PRD 07](07-twenty-four.md). It lives at `/twentyfour`, also answers the legacy
`/questions`, and is reachable from the main navigation.

Unlike the archive it replaced, it is **endlessly replayable without touching the
teaser bank** — it generates its own puzzles, so playing it all afternoon cannot
exhaust the one-a-day content.

## Consequences

- **Accepted:** a first-time visitor gets exactly one puzzle, which is thin for
  someone who wants to explore. That is the deliberate trade — the daily habit is
  worth more than the depth.
- Per-teaser statistics ([PRD 06](06-community-feedback.md)) now accumulate only
  from the daily page, so figures are effectively day-of totals rather than
  lifetime ones.
- Solutions are still gated on time rather than effort ([PRD 03](03-hints-and-solutions.md)).
  With recycling, the only published solution is the one on `/yesterday`, and which
  teaser that is follows the rotation rather than the calendar.

## Non-goals

- A curated "best of" or themed selection (a possible future feature, deliberately
  not smuggled in here)
- Redirecting old per-question URLs to the placeholder — a 404 is the honest answer
- Numbering puzzles ("#12") — dates remain the identifier

## Acceptance criteria

- [x] `/questions` and `/twentyfour` both serve the Twenty Four **game**
      ([PRD 07](07-twenty-four.md)) — this criterion read "maintenance page" long
      after the game replaced the placeholder
- [x] `/questions/{any-date}` returns **404** with no answer box (verified for past
      and future dates)
- [x] The menu offers "Twenty Four" and no route to browse teasers
- [x] The About page no longer promises past questions with solutions
- [x] `/` still serves the daily challenge normally
- [x] All teasers remain in the data and in admin

## Implementation notes

`Pages/TwentyFour.razor` carries both `@page "/twentyfour"` and `@page "/questions"`.
`Pages/Questions.razor` and `Pages/QuestionDetail.razor` were deleted, which is what
makes the per-date routes 404 — the shared `ChallengeView` is now rendered only by
the home page.

> **Reintroducing an archive** would mean re-adding a page that renders
> `ChallengeView` for a chosen teaser. Before doing so, revisit the problem
> statement above: whatever comes back should not let a visitor clear the bank in
> one sitting.

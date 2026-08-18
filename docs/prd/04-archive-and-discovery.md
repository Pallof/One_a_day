# PRD 04 — No archive (one a day means one a day)

**Status:** Spec of record · **Routes:** `/twentyfour` (and legacy `/questions`)
· **Supersedes:** the Archive & discovery spec, removed 2026-08-17

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
5. The daily challenge at `/` is unaffected: it still falls back to the most recent
   past teaser when today's is missing ([PRD 01](01-daily-challenge.md)).
6. **Teaser data is untouched.** Every teaser stays in `teasers.json` and remains
   visible in admin — only the public browsing surface is gone, so a curated
   archive can be reintroduced later without re-authoring anything.

### Placeholder page — "Twenty Four"

- Lives at `/twentyfour`, and also answers `/questions`.
- Reachable from the dropdown menu, replacing the old "More questions" entry.
- Currently states plainly that it is **under maintenance**, with a link back to
  today's challenge.
- The name reserves the slot for a Twenty Four feature; its behaviour is not yet
  specified.

## Consequences

- **Accepted:** a first-time visitor gets exactly one puzzle, which is thin for
  someone who wants to explore. That is the deliberate trade — the daily habit is
  worth more than the depth.
- Per-teaser statistics ([PRD 06](06-community-feedback.md)) now accumulate only
  from the daily page, so figures are effectively day-of totals rather than
  lifetime ones.
- Solutions are still gated on time rather than effort ([PRD 03](03-hints-and-solutions.md)),
  but a past teaser's solution is only reachable while it is standing in as the
  daily fallback.

## Non-goals

- A curated "best of" or themed selection (a possible future feature, deliberately
  not smuggled in here)
- Redirecting old per-question URLs to the placeholder — a 404 is the honest answer
- Numbering puzzles ("#12") — dates remain the identifier

## Acceptance criteria

- [x] `/questions` and `/twentyfour` both serve the Twenty Four maintenance page
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

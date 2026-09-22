# PRD 04 — No archive (one a day means one a day)

**Status:** Spec of record · **Routes:** `/twentyfour` (and the old `/questions`)
· **Supersedes:** the Archive & discovery spec, removed 2026-08-17

## In plain terms

**There is no way to browse old puzzles — on purpose.** The premise is one puzzle a day: the daily
rhythm makes the habit, and makes a single teaser worth thinking about. An archive let a visitor
burn through the whole bank in one sitting and then have no reason to come back. It also showed
scheduled teasers as a "sneak peek", so tomorrow's was never a surprise. It worked exactly as
specified — the feature itself was at odds with the goal, so it was removed rather than tuned.

Old puzzle addresses now say "not found" rather than merely being unlinked, because an unlinked
page is still one guessed address away. The archive's slot holds the Twenty Four game
([PRD 07](07-twenty-four.md)), which makes its own puzzles and can be played all afternoon without
using any up.

**Decision:** past and future teasers can't be browsed or replayed. The only puzzle a solver can
reach is today's.

## Requirements

1. No list of teasers and no page per teaser.
2. Past and future teasers are **unreachable by address**: the old `/questions/{date}` pages
   return **404** ("not found").
3. Nothing advertises browsing past questions — the About page included.
4. Old links to `/questions` land somewhere sensible rather than a dead end: the Twenty Four game.
5. The daily challenge is unaffected: a day with nothing scheduled still gets a puzzle, now by
   recycling ([PRD 08](08-recycling-rotation.md)) rather than the date-based fallback this doc
   first described.
6. **Teaser data is untouched.** Every teaser stays in `teasers.json` and in admin, so a curated
   archive could return without rewriting anything.

## Consequences

- **Accepted:** a first-time visitor gets exactly one puzzle, which is thin for someone who wants
  to explore. The daily habit is worth more than the depth.
- Statistics ([PRD 06](06-community-feedback.md)) now come only from the daily page, so they're
  on-the-day totals rather than lifetime ones.
- Solutions are still earned by time, not effort ([PRD 03](03-hints-and-solutions.md)); the only
  one published is on `/yesterday`, and which teaser that is follows the rotation, not the
  calendar.

## Non-goals

- A curated "best of" or themed selection — possible later, deliberately not smuggled in here
- Redirecting old per-question addresses to the game — a 404 is the honest answer
- Numbering puzzles ("#12") — dates remain the identifier

## Acceptance criteria

- [x] `/questions` and `/twentyfour` both serve the Twenty Four **game** ([PRD 07](07-twenty-four.md))
      — *this read "maintenance page" long after the game replaced the placeholder*
- [x] `/questions/{any-date}` returns **404** with no answer box (verified for past and future dates)
- [x] The menu links the game (labelled "24") and offers no route to browse teasers
- [x] The About page no longer promises past questions with solutions
- [x] `/` still serves the daily challenge normally
- [x] All teasers remain in the data and in admin

## Implementation notes

`Components/Pages/TwentyFour.razor` answers both `/twentyfour` and `/questions`. Deleting
`Questions.razor` and `QuestionDetail.razor` is what makes the per-date addresses 404;
`ChallengeView` is now used only by the home page.

> **Reintroducing an archive** means adding back a page that shows `ChallengeView` for a chosen
> teaser. Re-read the problem first: whatever comes back must not let a visitor clear the bank in
> one sitting.

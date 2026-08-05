# PRD 04 — Archive & discovery

**Status:** Spec of record · **Routes:** `/questions`, `/questions/{yyyy-MM-dd}`

## Problem

A daily puzzle accumulates a back catalogue that is the site's real depth — a new
visitor should have more than one puzzle to do. Early on the archive rendered every
question with its own answer box on a single page, which was cluttered and made each
puzzle feel disposable. It also gave away solutions with a single click, so nothing
was earned.

## Requirements

### Index (`/questions`)

- Lists **all** teasers — past, today, and scheduled — newest first.
- Each row is a **clickable link** showing the date, difficulty badge, and question
  text. Rows must not contain answer boxes.
- A row for a teaser with a support image shows a small 🖼️ indicator.
- Rows are labelled by state: today's is marked *"Today's challenge"* and links to
  `/`; scheduled ones are marked *"Scheduled"*.
- A **search box** filters by question text, case-insensitively, as the user types.
  Backend tags are **not** searchable here — they are invisible to solvers, so
  matching on them would be inexplicable.
- Answers, hints, and solutions must not appear in this list.

### Question page (`/questions/{yyyy-MM-dd}`)

- Each teaser has a **stable, shareable URL keyed by its date**.
- The page renders the **same format as the daily challenge** — question box,
  difficulty badge, concealable hint, submission box, feedback — so a past puzzle is
  fully playable, not just readable.
- An **"← All questions"** link sits at the **top** of the page, directly beneath the
  header.
- **Past** teaser: the solution may be revealed, since the challenge has closed ([PRD 03](03-hints-and-solutions.md)).
- **Scheduled** teaser: viewable as a sneak peek with an "Upcoming challenge" banner
  and an explanatory note; attempts are allowed, but the solution can **never** be
  revealed before its date.
- An unparseable date, or a date with no teaser, shows a plain "Question not found"
  page — not an error.

### Attempts count everywhere

Submissions on archived questions feed the same per-teaser statistics as the daily
challenge, so figures are lifetime totals rather than day-of totals.

## Non-goals

- Filtering the index by difficulty or tag (not yet warranted at ~17 teasers)
- Pagination (revisit past a few hundred teasers)
- Numbering puzzles ("#12") — dates are the identifier

## Acceptance criteria

- [x] Index is a link list with no inline answer boxes
- [x] Each past question opens its own page in daily-challenge format
- [x] Scheduled questions are listed and viewable but never reveal answers
- [x] Search filters on question text as you type
- [x] Bad/unknown dates render "Question not found"

## Implementation notes

`Pages/Questions.razor` (index) and `Pages/QuestionDetail.razor` (per-question).
The detail page loads any parseable date and passes `CanReveal = Date < today`,
which is what keeps a sneak peek from becoming a spoiler. Both render the shared
`ChallengeView`.

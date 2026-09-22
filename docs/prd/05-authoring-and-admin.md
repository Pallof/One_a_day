# PRD 05 — Authoring & admin

**Status:** Spec of record · **Route:** `/admin`, on the author's machine only

## In plain terms

One person writes every puzzle by hand, so adding one has to be fast. The realistic failure isn't
a bug — it's the author not getting round to it, the queue running dry, and the site breaking its
only promise. This page is the form: question, accepted answers, and an optional hint, explanation
and picture. The date defaults to the next empty day, so a week can be queued in one sitting.

It exists **only on the author's computer**; teasers reach the live site by copying `teasers.json`
across ([PRD 10](10-admin-authentication.md)). So the review queues and rotation reset below act on
the author's copy of the data, not the live site's.

## Requirements

### Teaser fields

| Field | Required | Behaviour |
|---|---|---|
| **Show on date** | yes | The day it becomes the challenge. Defaults to the **next date with no teaser**, so repeated adds queue forward. |
| **Difficulty** | yes (default Medium) | Easy / Medium / Hard, shown to solvers as a pill with a green, gold or red dot ([PRD 09](09-visual-design.md)). |
| **Question** | yes | The teaser text. |
| **Answer** | yes | Accepted answers, separated by `;` ([PRD 02](02-answer-evaluation.md)). |
| **Tags** | no | Labels for the author's own sorting. **Never shown to solvers.** |
| **Support image** | no | For puzzles that need a diagram. |
| **Hint** | no | Unlocks after the first attempt ([PRD 03](03-hints-and-solutions.md)). |
| **Solution** | no | Shown on solving, and on `/yesterday` the day after the challenge runs. |

### Scheduling

- Two teasers never share a date: trying must **warn and refuse**, never silently overwrite.
- Dates may be any distance ahead.
- Gaps are allowed — recycling fills them ([PRD 08](08-recycling-rotation.md)), so solvers never see
  a dry spell. **The author still needs to know**, which is what the rotation panel is for.

### Support images

- **PNG, JPG, GIF or WebP**, up to **3 MB**, both checked on the server.
- Saved in `App_Data/teaser-images/` under a **random file name** — the uploaded name is never
  trusted or reused — and served read-only from `/teaser-images/{name}`.
- No files left behind: replacing an image deletes the old one, "Remove image" clears it, deleting
  a teaser deletes its image, and an upload abandoned without saving is cleaned up.

### Teaser table

- Every teaser, scheduled and past: date, difficulty, question, tags, answer, statistics, and a
  **Shown** count with its current draw weight ([PRD 08](08-recycling-rotation.md)). Today's row
  is highlighted; future rows are faded.
- **Edit** loads every field back into the form; **Delete** removes the teaser, its statistics and
  its image.
- Wide tables scroll **inside their own box** and never widen the page ([PRD 09](09-visual-design.md)).

### Rotation panel

The recycling box at a glance, without opening `rotation.json`:

- Slips left, bank size, and the refill point
- **How many new teasers are scheduled ahead** — the real sign of a queue running dry, since
  solvers never see a gap
- The last 14 days: which teaser ran, and whether it was `new` or `recycled`
- **Reset the box now**, starting a fresh cycle over the whole bank

### Review queues

Suggestions and issue reports are reviewed here too ([PRD 06](06-community-feedback.md)).

## Non-goals

- Several authors, roles, or an edit history
- Formatting in questions — plain text only
- Scheduling by rule (e.g. "hard puzzles on Fridays")

## Acceptance criteria

- [x] Date defaults to the next free day; duplicate dates are refused with a warning
- [x] All fields round-trip through Edit without loss
- [x] Image type/size validated server-side; orphaned files cleaned up
- [x] Tags visible in admin, never rendered to solvers
- [x] Deleting a teaser removes its stats and image

## Implementation notes

`Components/Pages/Admin.razor`, with `TeaserStore`, `StatsStore`, `ImageStore`, `SuggestionStore`,
`IssueStore` and `RotationStore`. Uploaded images need their own `UseStaticFiles` line in
`Program.cs`, because `MapStaticAssets` only serves files that existed at build time. Admin is the
one page on the **wide** layout, because its tables don't fit the 680px reading column
([PRD 09](09-visual-design.md)).

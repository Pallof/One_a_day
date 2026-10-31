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
| **Ruled wrong** | no | Answers the author has decided are wrong, separated by `;`. Jev is never asked about them, and they stay wrong whatever Jev said before ([PRD 02](02-answer-evaluation.md#the-second-opinion-jev)). Kept when the teaser is edited and saved. |
| **Tags** | no | Labels for the author's own sorting. **Never shown to solvers.** |
| **Support image** | no | A picture under the question: a diagram that helps people picture the puzzle, or just something to make them smile. Every question gets one, made and reviewed as [docs/brand/puzzle-pictures.md](../brand/puzzle-pictures.md) describes *(author's decision, 2026-10-23; from 2026-10-18 it was a nice-to-have)*. The field stays optional, so a question can go in before its picture is ready. |
| **Picture description** | with a picture | What the picture shows, in words. Screen readers read it out, and email apps that hold pictures back show it instead. **Add teaser** stays disabled until it's written. |
| **Hint** | no | Kept with the teaser; the site doesn't show it (2026-10-27, [PRD 03](03-hints-and-solutions.md)). |
| **Solution** | no | Shown on solving, and on `/yesterday` the day after the challenge runs. |
| **Suggested by** | no | The visitor to credit for a suggested teaser, saved as `SuggestedBy` in `teasers.json` *(author's decision, 2026-10-30)*. **Use in form above** fills it from the name on the suggestion; blank saves as none, and teasers saved before the field load as none. Shown under today's question as *"Today's question was given by Sam"* ([PRD 01](01-daily-challenge.md)), so check it before the teaser goes live. |

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
- **Every picture is described in words** *(added 2026-10-18)*. A diagram's description carries
  every number and label on it, since someone who can't see the picture needs them to solve the
  puzzle. Until then every picture was read out as "Support image for this teaser". One saved
  before the field existed is read out as "A picture that goes with the question".
- **Aim for under 200 KB.** A bigger picture still saves, with a note that it will load slowly on
  a phone. 1200×675 matches the link-preview card and suits both the page and the email.
- **A picture must never hint at the answer.** The poles puzzle draws its poles well apart and
  marks the distance `d = ?`.
- How the pictures are made, and the house style for cartoons: [docs/brand/puzzle-pictures.md](../brand/puzzle-pictures.md).
- On the live site a picture goes up with the bank, by hand for now
  ([PRD 11](11-deployment.md), requirement 13).

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

**Answers Jev judged** is optional reading: Jev's verdicts take effect as soon as they're made,
and nothing here waits on the author. Its header counts them as "3 new from Jev", not as a to-do
(author's decision, 2026-10-28). It lists the answers the checker turned down and Jev then judged, newest
first, leaving out any the author has already dealt with and any made before the question was
last edited. Each shows what was typed, the question, Jev's chance and whether it counts as right
at today's bar, with two buttons:
- **Agree with Jev** clears the row and changes nothing else. It's remembered in
  `agreed-verdicts.json` on this Mac only.
- **Rule it wrong**, shown only where the answer counts as right, adds what was typed to the
  teaser's *Ruled wrong* list. A `;` in it becomes a
  `,`, so it stays one ruling.

Nothing a visitor typed can be made an accepted answer from here: to pass an answer Jev turned
down, add it in your own words with Edit. The list is this Mac's copy: `deploy/pull-live-data.sh`
fetches the live one ([PRD 02](02-answer-evaluation.md#the-second-opinion-jev)).

## Non-goals

- Several authors, roles, or an edit history
- Formatting in questions — plain text only
- Scheduling by rule (e.g. "hard puzzles on Fridays")

## Acceptance criteria

- [x] Date defaults to the next free day; duplicate dates are refused with a warning
- [x] All fields round-trip through Edit without loss
- [x] A suggestion's name goes into Suggested by and on into the bank; editing a credited teaser
      keeps it, and emptying it saves none (`AdminLayoutTests`, mutation-verified)
- [x] Image type/size validated server-side; orphaned files cleaned up
- [x] Tags visible in admin, never rendered to solvers
- [x] Deleting a teaser removes its stats and image
- [x] **Picture descriptions** — `TeaserPictureTests`, each mutation-verified: a picture can't be
      saved without one, and the one written is stored; Edit brings it back, so Save isn't left
      disabled; the challenge and yesterday's page read it out instead of a stock phrase

## Implementation notes

`Components/Pages/Admin.razor`, with `TeaserStore`, `StatsStore`, `ImageStore`, `SuggestionStore`,
`IssueStore` and `RotationStore`. Uploaded images need their own `UseStaticFiles` line in
`Program.cs`, because `MapStaticAssets` only serves files that existed at build time. Admin is the
one page on the **wide** layout, because its tables don't fit the 680px reading column
([PRD 09](09-visual-design.md)).

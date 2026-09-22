# PRD 05 — Authoring & admin

**Status:** Spec of record · **Route:** `/admin`, on the author's machine only

## In plain terms

One person writes every puzzle by hand, so adding one has to be fast — if it's tedious the
queue runs dry and the site breaks its only promise. This page is the form for that: the
question, the accepted answers, an optional hint, solution and picture. It defaults the date to
the next empty day, so you can queue a week in one sitting without retyping anything.

It exists **only on the author's own computer**. The live site has no admin page at all.

## Problem

The product's whole premise is one new teaser per day, published by one person by
hand. If authoring has any friction, the queue runs dry and the site breaks its only
promise. The realistic failure mode is not a bug — it's the author not getting around
to it.

> **This page exists only on the author's machine.** The live site has no `/admin`;
> teasers written here are published by copying `teasers.json` — see
> [PRD 10](10-admin-authentication.md). The review queues and the rotation reset below
> act on this machine's copy of that data, not the live site's.

## Requirements

### Teaser fields

| Field | Required | Behaviour |
|---|---|---|
| **Show on date** | yes | The day it becomes the challenge. Defaults to the **next date with no teaser**, so repeated adds queue forward without retyping. |
| **Difficulty** | yes (defaults Medium) | Easy / Medium / Hard. Rendered to solvers as a tinted pill with a coloured dot — green / gold / red ([PRD 09](09-visual-design.md)). |
| **Question** | yes | The teaser text. |
| **Answer** | yes | Accepted answers, `;`-separated ([PRD 02](02-answer-evaluation.md)). |
| **Tags** | no | Comma-separated labels for the author's own classification. **Never shown to solvers** — admin-only, for sorting and spotting themes. |
| **Support image** | no | For puzzles needing a diagram. |
| **Hint** | no | Unlocks after the solver's first attempt. |
| **Solution** | no | Shown on solve, or revealable the day after the challenge runs. |

### Scheduling rules

- Two teasers must never occupy the same date. Attempting it must **warn and refuse**
  rather than silently overwrite.
- Dates may be scheduled arbitrarily far ahead.
- A gap in the schedule is allowed. It is no longer a degraded case: the recycling
  box fills it with a past teaser ([PRD 08](08-recycling-rotation.md)), so a dry
  stretch is invisible to solvers. **The author still needs to know**, which is what
  the rotation panel below is for.

### Support images

- Accepted types: **PNG, JPG, GIF, WebP**; max **3 MB**. Both checks enforced
  server-side.
- Stored under `App_Data/teaser-images/` with a **random GUID filename** — the
  uploader's filename is never trusted or reused on disk.
- Served read-only from `/teaser-images/{name}`.
- Lifecycle must not leak files: replacing an image deletes the old one, "Remove
  image" clears it, deleting a teaser deletes its image, and an upload abandoned
  without saving is cleaned up.

### Management table

- Lists every teaser (scheduled and past) with date, difficulty, question, tags,
  answer, per-teaser stats, and a **Shown** count with the teaser's current draw
  weight ([PRD 08](08-recycling-rotation.md)).
- Today's row is highlighted; future rows are visually de-emphasised.
- **Edit** loads a teaser back into the form (carrying every field, including
  difficulty, tags, and image); **Delete** removes the teaser, its stats, and its image.
- Wide tables scroll **inside their own container**; they must never widen the page
  itself ([PRD 09](09-visual-design.md)).

### Rotation panel

Surfaces the recycling box so the author can see it working without reading
`rotation.json`:

- Slips remaining, bank size, and the refill threshold.
- **How many new teasers are scheduled ahead** — the real signal for whether the
  queue is running dry, and the reason a gap is no longer visible to solvers.
- The last 14 days: date, which teaser ran, and whether it was `new` or `recycled`.
- **Reset the box now**, starting a fresh cycle over the whole bank.

### Review queues

The admin page also hosts the two inbound queues — visitor suggestions and issue
reports — specified in [PRD 06](06-community-feedback.md).

## Non-goals

- Multiple authors, roles, or an edit history
- Rich-text or Markdown in questions (plain text only)
- Scheduling by rule (e.g. "hard puzzles on Fridays")

## Acceptance criteria

- [x] Date defaults to the next free day; duplicate dates are refused with a warning
- [x] All fields round-trip through Edit without loss
- [x] Image type/size validated server-side; orphaned files cleaned up
- [x] Tags visible in admin, never rendered to solvers
- [x] Deleting a teaser removes its stats and image

## Implementation notes

`Pages/Admin.razor` with `TeaserStore`, `StatsStore`, `ImageStore`, `SuggestionStore`,
`IssueStore`, and `RotationStore`. Runtime-uploaded images need an explicit
`UseStaticFiles` mapping in `Program.cs` — the .NET template's `MapStaticAssets` only
serves build-time `wwwroot` content, not files written after build.

Admin is the one route that opts into the **wide** measure rather than the reading
column, because its tables don't fit 680px ([PRD 09](09-visual-design.md)).

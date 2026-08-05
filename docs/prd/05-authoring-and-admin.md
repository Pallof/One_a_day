# PRD 05 — Authoring & admin

**Status:** Spec of record · **Route:** `/admin`

## Problem

The product's whole premise is one new teaser per day, published by one person by
hand. If authoring has any friction, the queue runs dry and the site breaks its only
promise. The realistic failure mode is not a bug — it's the author not getting around
to it.

> ⚠️ **This page currently has no authentication.** That is acceptable while the app
> runs on localhost and is a **launch blocker** — see
> [PRD 10](10-admin-authentication.md).

## Requirements

### Teaser fields

| Field | Required | Behaviour |
|---|---|---|
| **Show on date** | yes | The day it becomes the challenge. Defaults to the **next date with no teaser**, so repeated adds queue forward without retyping. |
| **Difficulty** | yes (defaults Medium) | Easy / Medium / Hard. Rendered to solvers as a colour-coded badge: green / yellow / red. |
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
- A gap in the schedule is allowed; the daily page degrades gracefully
  ([PRD 01](01-daily-challenge.md)).

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
  answer, and per-teaser stats.
- Today's row is highlighted; future rows are visually de-emphasised.
- **Edit** loads a teaser back into the form (carrying every field, including
  difficulty, tags, and image); **Delete** removes the teaser, its stats, and its image.

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

`Pages/Admin.razor` with `TeaserStore`, `StatsStore`, `ImageStore`. Runtime-uploaded
images need an explicit `UseStaticFiles` mapping in `Program.cs` — the .NET template's
`MapStaticAssets` only serves build-time `wwwroot` content, not files written after
build.

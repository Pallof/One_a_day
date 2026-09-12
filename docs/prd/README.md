# One a Day — Product Requirements

This folder holds the product requirements documents for **One a Day**, a daily
brain teaser web app.

Two kinds of document live here — the table below says which each one is. The number
is only the order they were written in, not the kind.

- **Spec of record** — describes behaviour that is **built and shipped**.
  These are the reference for how the product is supposed to work; if the code and
  the doc disagree, one of them is a bug.
- **Proposals** — features **not yet built**. These define the problem,
  the requirements, and the acceptance criteria before implementation starts.

| Doc | Status |
|---|---|
| [00 — Product overview](00-product-overview.md) | Spec of record |
| [01 — Daily challenge](01-daily-challenge.md) | Spec of record |
| [02 — Answer evaluation](02-answer-evaluation.md) | Spec of record |
| [03 — Hints & solutions](03-hints-and-solutions.md) | Spec of record |
| [04 — No archive](04-archive-and-discovery.md) | Spec of record |
| [05 — Authoring & admin](05-authoring-and-admin.md) | Spec of record |
| [06 — Community feedback](06-community-feedback.md) | Spec of record |
| [07 — Twenty Four game](07-twenty-four.md) | Spec of record |
| [08 — Recycling rotation](08-recycling-rotation.md) | Spec of record |
| [09 — Visual design system](09-visual-design.md) | Spec of record |
| [10 — Admin authentication](10-admin-authentication.md) | **Proposed — blocker for launch** |
| [11 — Deployment](11-deployment.md) | Proposed |
| [12 — Streaks & sharing](12-streaks-and-sharing.md) | Proposed |
| [13 — Content pipeline](13-content-pipeline.md) | Proposed |
| [14 — Email notifications](14-email-notifications.md) | Spec of record |
| [15 — Email subscriptions](15-email-subscriptions.md) | Spec of record |

## Conventions

- **Must / should / may** carry their usual RFC-style weight.
- "Solver" = a visitor answering puzzles. "Author" = the person publishing them
  (currently a single person, the site owner).
- Dates and day boundaries always mean **Pacific Time** — see
  [01 — Daily challenge](01-daily-challenge.md).

### Keeping these honest

A spec of record that has drifted is worse than no spec, because people trust it. Two
rules earned the hard way in an audit of all of these on 2026-09-08:

- **A `[x]` means verified, not intended.** Three ticked criteria were false — a
  "dated note" that had been deliberately removed, a "maintenance page" replaced by a
  shipped game, and a test count off by 140. Each one stopped anyone from looking.
- **Behaviour changes in the same pass as the doc.** Every inconsistency found traced
  to work that shipped without a doc pass beside it. The specs that were current were
  current because they were edited *while* the feature was built.

Don't pin numbers that drift on their own (total test counts, file sizes). Pin the
thing that proves the behaviour instead.

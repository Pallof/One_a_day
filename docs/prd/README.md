# One a Day — Product Requirements

This folder holds the product requirements documents for **One a Day**, a daily
brain teaser web app.

Two kinds of document live here:

- **Spec of record (`0x-`)** — describes behaviour that is **built and shipped**.
  These are the reference for how the product is supposed to work; if the code and
  the doc disagree, one of them is a bug.
- **Proposals (`1x-`)** — features **not yet built**. These define the problem,
  the requirements, and the acceptance criteria before implementation starts.

| Doc | Status |
|---|---|
| [00 — Product overview](00-product-overview.md) | Spec of record |
| [01 — Daily challenge](01-daily-challenge.md) | Spec of record |
| [02 — Answer evaluation](02-answer-evaluation.md) | Spec of record |
| [03 — Hints & solutions](03-hints-and-solutions.md) | Spec of record |
| [04 — Archive & discovery](04-archive-and-discovery.md) | Spec of record |
| [05 — Authoring & admin](05-authoring-and-admin.md) | Spec of record |
| [06 — Community feedback](06-community-feedback.md) | Spec of record |
| [10 — Admin authentication](10-admin-authentication.md) | **Proposed — blocker for launch** |
| [11 — Deployment](11-deployment.md) | Proposed |
| [12 — Streaks & sharing](12-streaks-and-sharing.md) | Proposed |
| [13 — Content pipeline](13-content-pipeline.md) | Proposed |

## Conventions

- **Must / should / may** carry their usual RFC-style weight.
- "Solver" = a visitor answering puzzles. "Author" = the person publishing them
  (currently a single person, the site owner).
- Dates and day boundaries always mean **Pacific Time** — see
  [01 — Daily challenge](01-daily-challenge.md).

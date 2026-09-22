# Stumpty — Product Requirements

Requirements for **Stumpty**, a daily brain-teaser website. Each doc is both a spec and a diary:
how a feature works, plus the decisions and mistakes that shaped it.

- **Spec of record** — built and shipped. If the code and the doc disagree, one of them is a bug.
- **Proposed** — not built yet: the problem, the requirements, and how we'll know it's done.

The number is only the order the docs were written in.

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
| [10 — Admin access](10-admin-authentication.md) | Spec of record |
| [11 — Deployment](11-deployment.md) | Proposed |
| [12 — Streaks & sharing](12-streaks-and-sharing.md) | Proposed |
| [13 — Content pipeline](13-content-pipeline.md) | Proposed |
| [14 — Email notifications](14-email-notifications.md) | Spec of record |
| [15 — Email subscriptions](15-email-subscriptions.md) | Spec of record |

## Conventions

- Each doc opens with **In plain terms**. **Implementation notes** at the end are for developers.
- **Must** is a hard rule, **should** a strong default, **may** optional.
- **Solver** = a visitor answering puzzles. **Author** = the person publishing them — today, the
  site owner alone.
- Dates and day boundaries always mean **Pacific Time**.
- **History** notes record past decisions and mistakes. They're kept on purpose.
- **Author's decision** (dated) marks a product call rather than a technical limit — open to
  revisiting, but not an accident.
- **Mutation-verified**: the safeguard was deliberately broken to confirm its test fails. A test
  that passes whether or not the code works is worse than none — it stops anyone looking.
- **Settings are written two ways.** `Email:AppPassword` in the settings file or user-secrets and
  `Email__AppPassword` as a server's environment variable are the same setting: environment
  variables can't contain a colon, so it becomes a double underscore.

### Keeping these honest

A drifted spec is worse than none, because people trust it. An audit of every doc on 2026-09-08
set two rules:

- **`[x]` means verified, not intended.** Three ticked criteria were false: a "dated note" removed
  on purpose, a "maintenance page" long since replaced by a game, and a test count off by 140.
- **Change the doc in the same pass as the behaviour.** Every inconsistency traced to work shipped
  without a doc edit beside it; the docs that stayed accurate were edited while the feature was
  built.

Don't pin numbers that drift on their own, such as total test counts or file sizes. Pin the thing
that proves the behaviour.

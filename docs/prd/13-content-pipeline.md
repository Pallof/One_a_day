# PRD 13 — Content pipeline

**Status:** Proposed · **Priority: P2 — author quality of life**

## Problem

The product promises a puzzle every day, and one person supplies them by hand. The
most likely way the site fails is not a crash — it's the author not getting to it on a
busy Thursday. Nothing currently warns that the queue is about to run dry.

This is not hypothetical: during development the schedule ran out for four days, and
the only signal was the home page quietly showing *"today's teaser hasn't been posted
yet"* with an older puzzle. The fallback worked; the warning didn't exist.

Adding teasers is also strictly one-at-a-time, which makes stocking a week more
tedious than it needs to be.

## Goals

- Make an empty upcoming queue impossible to miss.
- Let the author add many teasers in one sitting.

## Non-goals

- Generating puzzles automatically
- Scheduling rules (e.g. "Hard on Fridays")
- Multi-author workflow or approvals

## Requirements

### Dry-queue warning

1. Admin must show a prominent warning when fewer than **N days** (default 3) of
   *upcoming* teasers are scheduled, naming the last scheduled date.
2. The warning must state the specific gap — e.g. *"⚠️ Nothing scheduled after Sun 3
   Aug. Tomorrow is empty."*
3. It must distinguish **"tomorrow is empty"** (urgent) from **"running low"** (soon).
4. A **gap in the middle** of the schedule (a skipped date between two scheduled ones)
   must also be surfaced, since the fallback hides it from solvers.
5. Optional stretch: a discreet reminder for the author only — not shown to solvers.

### Bulk import

6. Accept a paste of multiple teasers in a simple text or JSON format and create them
   all at once.
7. Auto-assign each to the **next free date** in order, skipping occupied dates.
8. Show a **preview** of what will be created — date, difficulty, question, answer —
   and require confirmation before writing.
9. Validate every row before importing any: required fields present, difficulty
   recognised, no duplicate dates. On any error, import nothing and report the row.
10. The paste format should mirror the plain-text style the author already writes in
    (`Question:` / `Hint:` / `Solution:` / `Tag:` / `Difficulty:` blocks), so notes
    can be pasted directly.

### Duplicate detection

11. Warn when a new question closely matches an existing one. Two near-identical
    average-speed puzzles were added and only caught by eye later.

## Acceptance criteria

- [ ] Admin warns when tomorrow has no teaser, and when the queue is under N days
- [ ] Mid-schedule gaps are reported
- [ ] Pasting a multi-teaser block previews all rows before writing
- [ ] Import assigns sequential free dates and never double-books
- [ ] A malformed row aborts the whole import with a clear message
- [ ] Near-duplicate questions raise a warning at save time

## Open questions

- Threshold for "running low": 3 days, or a week? Suggest 3 to start.
- Should duplicate detection block saving or merely warn? Suggest warn — the author
  may want deliberate variations.

# PRD 13 — Content pipeline

**Status:** Proposed · **Priority: P2 — author quality of life**

## In plain terms

**Not built yet — this is a proposal.** The site promises a puzzle every day and one person
supplies them by hand, so the likeliest failure isn't a crash — it's the author not getting to it
on a busy Thursday. Three conveniences would help:

- **A warning before the queue runs out.** During development the schedule ran dry for four days,
  and the only sign was the home page quietly showing an older puzzle with *"today's teaser hasn't
  been posted yet"*. The fallback worked; the warning didn't exist. Admin now counts the teasers
  scheduled ahead ([PRD 05](05-authoring-and-admin.md)), but only if the author looks — and
  recycling hides any gap from solvers.
- **Pasting in a whole week at once**, instead of adding puzzles one at a time.
- **A nudge when a new question looks like an existing one** — two near-identical average-speed
  puzzles slipped in and were only spotted by eye later.

## Goals

- Make an empty upcoming queue impossible to miss.
- Let the author add many teasers in one sitting.

## Non-goals

- Generating puzzles automatically
- Scheduling rules (e.g. "Hard on Fridays")
- Several authors, or approvals

## Requirements

### Running-dry warning

1. Admin shows a prominent warning when fewer than **N days** (default 3) of *upcoming* teasers are
   scheduled, naming the last scheduled date.
2. It states the gap — e.g. *"⚠️ Nothing scheduled after Mon 3 Aug. Tomorrow is empty."*
3. It tells **"tomorrow is empty"** (urgent) apart from **"running low"** (soon).
4. A **gap mid-schedule** (a skipped date between two scheduled ones) is flagged too, since
   recycling hides it from solvers.
5. Optional: a quiet reminder for the author only — never shown to solvers.

### Bulk import

6. Accept a paste of several teasers, as simple text or JSON, and create them all at once.
7. Give each the **next free date** in order, skipping taken dates.
8. **Preview** what will be created — date, difficulty, question, answer — and require confirmation
   before saving.
9. Check every row before importing any: required fields present, difficulty recognised, no
   duplicate dates. On any error, import nothing and name the row.
10. Match the plain-text layout the author already writes in (`Question:` / `Hint:` / `Solution:` /
    `Tag:` / `Difficulty:` blocks), so notes paste straight in.

### Duplicate detection

11. Warn when a new question closely matches an existing one.

## Acceptance criteria

- [ ] Admin warns when tomorrow has no teaser, and when the queue is under N days
- [ ] Mid-schedule gaps are reported
- [ ] Pasting a multi-teaser block previews all rows before writing
- [ ] Import assigns sequential free dates and never double-books
- [ ] A malformed row aborts the whole import with a clear message
- [ ] Near-duplicate questions raise a warning at save time

## Open questions

- "Running low" threshold: 3 days or a week? Suggest 3 to start.
- Should a likely duplicate block saving, or just warn? Suggest warn — the author may want
  deliberate variations.

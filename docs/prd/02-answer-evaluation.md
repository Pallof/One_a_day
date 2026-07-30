# PRD 02 — Answer evaluation

**Status:** Spec of record · **Owner:** `Models/BrainTeaser.cs`

## Problem

Free-text answers are the core interaction, and the failure mode that would kill
trust fastest is telling a solver who *is* right that they're wrong. Puzzle answers
arrive in wildly different shapes — a word, a number, a number with a unit, a
spelled-out number, an algebraic formula — and the author cannot be expected to
enumerate every phrasing by hand.

Equally, generosity must not tip into accepting wrong answers.

## Requirements

### Authoring format

- An answer is a single string. **Alternative accepted answers are separated by
  `;`** — e.g. `48; 48 mph; forty-eight miles per hour`.
- Every alternative is evaluated independently under all rules below.

### Matching rules

1. **Text** — comparison ignores case, whitespace, and punctuation.
   `A Keyboard!` matches `a keyboard`.
2. **Numbers compare by value, not by text.**
   `9` = `9.0` = ` 9 `; `1,000` = `1000`; `-5` = `-5.0`.
   Numeric comparison must not be reachable by stripping punctuation — `9.0` must
   **not** collapse into `90`.
3. **Spelled-out numbers count as their value.**
   `eighty` = `80`, `forty-eight` = `forty eight` = `48`, `five thousand` = `5000`,
   `nine hundred and one` = `901`. This works in both directions (a stored word
   answer accepts digits). The common misspelling `fourty` is accepted.
4. **Number + unit** — a value and a unit are compared separately, so
   `eighty degrees` matches `80 degrees` while `eighty radians` does not.
5. **A single parenthetical group creates alternatives.**
   Storing `12 (a dozen)` accepts `12`, `a dozen`, or `12 (a dozen)`.
6. **Formulas are matched whole.** An answer with multiple or nested parentheses —
   e.g. `5*(5-(1/5))`, `8 / (3 - (8/3))` — must **not** be split by rule 5. A
   fragment such as `5` or `8` must be rejected.
7. **Blank or whitespace-only submissions are always rejected.**

### Guarantees

- Non-numeric words must never be coerced into numbers (`banana`, or `thousand`
  standing alone, are not numbers).
- The rules are **order-independent** and must hold for both stored answer and
  submission.

## Non-goals

- Symbolic/algebraic equivalence — `5*4.8` is **not** accepted for `5*(5-(1/5))`.
  Authors should list alternate forms explicitly with `;` if they want them.
- Fuzzy spelling correction on words (`kyboard` is wrong).
- Natural-language understanding of prose answers.

## Acceptance criteria

Every rule above is pinned by unit tests in `OneADay.Tests/`, and each shipped
teaser has explicit accept/reject cases:

- [x] `AnswerValidationTests.cs` — the rule matrix, including number words
- [x] `TeaserBankTests.cs` — every question in the live bank, accepted phrasings
      and plausible wrong answers
- [x] **97 tests passing**

> **Regression on record:** rule 5 originally used *first `(`* to *last `)`*, which
> made `5*(5-(1/5))` accept a bare `5`. Rule 6 exists because of that bug; the
> tests for it must not be removed.

## Implementation notes

All logic is in `BrainTeaser.AcceptsAnswer` and its private helpers
(`Variants`, `Matches`, `TryParseNumber`, `TryParseNumberWords`,
`TrySplitNumberAndUnit`). It is pure, in-memory, and has no dependencies — a
submission is never stored, logged, or interpolated into any query.

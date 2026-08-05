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
   - **All numeric comparison happens at the thousandths place.** Both sides are
     rounded to 3 decimals (away from zero at the midpoint) before comparing,
     because puzzle arithmetic rarely divides evenly. So a stored `0.333` accepts
     `1/3` and `0.3333333`, while `0.334` is still wrong.
   - Rounding applies only to the **final** value; intermediate steps inside an
     expression keep full precision, so errors don't compound.
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
7. **Formulas are compared by what they compute, not how they were typed** —
   but must use exactly the numbers the question supplied.
   The expression is evaluated (`+ - * /`, parentheses, decimals, unary sign,
   `×`/`÷`, and implicit multiplication such as `5(5-1/5)`), and a submission is
   accepted when **both** hold:
   1. it computes the same value as the author's answer, and
   2. it is built from **exactly the same multiset of numeric literals**.

   So for `5*(5-(1/5))` — a "make 24 from 5, 5, 5, 1" puzzle:

   | Submission | | Why |
   |---|---|---|
   | `5*(5-1/5)`, `(5-1/5)*5`, `5(5-1/5)`, `5 × (5 − 1/5)` | ✅ | same value, same numbers |
   | `24` | ❌ | not a formula |
   | `12+12`, `8*3`, `25-1` | ❌ | right value, **wrong numbers** |
   | `5 * (5 - 0.2)`, `5*4.8` | ❌ | folds `1/5` into a number not supplied |
   | `5*5-(5/5)` | ❌ | uses `5` four times; only three were given |

   - The operand check is a **hard requirement**: hitting the target value by any
     other route is not a solution to the puzzle.
   - Values are compared at the thousandths place (rule 2), which is what lets
     `8/(3-8/3)` count as 24 despite division residue.
   - A **plain-number** answer is exempt from the operand check, so a solver
     showing their work (`500*10` for `5000`) is still credited.
   - Malformed input (`5*(5-`, `((((`, `1/0`) is rejected, never thrown.

> **Authoring guidance.** Store an answer as a **formula** only when the formula
> *is* the puzzle (make 24 from these numbers). For a question whose answer is a
> value, store the **number** — `0.333`, not `1/3` — otherwise solvers must
> reproduce a formula using the same operands rather than simply giving the value.
8. **Blank or whitespace-only submissions are always rejected.**

### Guarantees

- Non-numeric words must never be coerced into numbers (`banana`, or `thousand`
  standing alone, are not numbers).
- The rules are **order-independent** and must hold for both stored answer and
  submission.

## Non-goals

- **Accepting a partially-simplified formula.** The operand check is strict by
  design, so a solver who folds a step into a new number (`5 * (5 - 0.2)`, or
  `5*4.8`) is rejected even though the arithmetic is sound. This is the accepted
  cost of guaranteeing that a "make 24 from these numbers" puzzle can only be
  solved with those numbers. Authors can always add such forms explicitly with `;`.
- Deriving the required numbers from the **question text**. They are taken from
  the author's stored answer, which is by definition a valid solution — no prose
  parsing involved.
- Symbolic algebra beyond arithmetic — no variables, exponents, or roots.
- Fuzzy spelling correction on words (`kyboard` is wrong).
- Natural-language understanding of prose answers.

## Acceptance criteria

Every rule above is pinned by unit tests in `OneADay.Tests/`, and each shipped
teaser has explicit accept/reject cases:

- [x] `AnswerValidationTests.cs` — the rule matrix, including number words and
      equivalent formula forms
- [x] `TeaserBankTests.cs` — every question in the live bank, accepted phrasings
      and plausible wrong answers
- [x] **144 tests passing**

> **Regression on record:** rule 5 originally used *first `(`* to *last `)`*, which
> made `5*(5-(1/5))` accept a bare `5`. Rule 6 exists because of that bug; the
> tests for it must not be removed.

## Implementation notes

All logic is in `BrainTeaser.AcceptsAnswer` and its private helpers
(`Variants`, `Matches`, `TryParseNumber`, `TryParseNumberWords`,
`TrySplitNumberAndUnit`, `TryEvaluateExpression`). It is pure, in-memory, and has
no dependencies — a submission is never stored, logged, or interpolated into any
query. The expression evaluator is a hand-written recursive-descent parser with a
depth cap, so hostile input (`((((((…`) is rejected rather than overflowing the
stack.

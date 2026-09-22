# PRD 02 — Answer evaluation

**Status:** Spec of record · **Owner:** `Models/BrainTeaser.cs`

## In plain terms

People type answers freely, and the same answer arrives in many shapes — a word, a number, a
number with a unit, a spelled-out number, a formula. The author can't list every phrasing, so the
site is generous about *how* an answer is written, without ever accepting a wrong one:

- `A Keyboard!` matches `a keyboard` — case and punctuation ignored
- `forty-eight` matches `48` — spelled-out numbers count as their value
- `9.0` matches `9`, and `1,000` matches `1000`
- But `12+12` does **not** solve "make 24 from 5, 5, 5 and 1" — right total, wrong numbers

Telling someone who *is* right that they're wrong would destroy trust fastest, so the rules lean
generous — never so far that a wrong answer gets through.

## Requirements

### Writing an answer

One line of text. **Alternative accepted answers are separated by `;`** — e.g.
`48; 48 mph; forty-eight miles per hour` — and each is checked under every rule below.

### Matching rules

1. **Text** ignores case, spaces and punctuation.
2. **Numbers compare by value:** `9` = `9.0` = ` 9 `, `1,000` = `1000`, `-5` = `-5.0`. Stripping
   punctuation must never be how numbers match — `9.0` must **not** become `90`.
   - **Values compare to three decimal places.** Both sides are rounded to 3 decimals (halves
     away from zero), because puzzle arithmetic rarely divides evenly: a stored `0.333` accepts
     `1/3` and `0.3333333`, while `0.334` is still wrong.
   - Only the final value is rounded; steps inside a formula keep full precision, so errors don't
     pile up.
3. **Spelled-out numbers count as their value**, both ways round: `eighty` = `80`, `forty-eight` =
   `forty eight` = `48`, `five thousand` = `5000`, `nine hundred and one` = `901`. The common
   misspelling `fourty` is accepted.
4. **Number + unit** compare separately: `eighty degrees` matches `80 degrees`; `eighty radians`
   doesn't.
5. **One pair of brackets creates alternatives:** storing `12 (a dozen)` accepts `12`, `a dozen`,
   or `12 (a dozen)`.
6. **Formulas are matched whole.** An answer with several or nested brackets — `5*(5-(1/5))`,
   `8 / (3 - (8/3))` — is **not** split by rule 5, so a fragment such as `5` or `8` is rejected.
7. **Formulas compare by what they work out to — but must use exactly the numbers given.** The
   site does the arithmetic (`+ - * /`, brackets, decimals, minus signs, `×` `÷`, and implied
   multiplication like `5(5-1/5)`) and accepts a formula only when it **both** reaches the
   author's value **and** uses exactly the same numbers, each as many times.

   For `5*(5-(1/5))`, the answer to "make 24 from 5, 5, 5, 1":

   | Submission | | Why |
   |---|---|---|
   | `5*(5-1/5)`, `(5-1/5)*5`, `5(5-1/5)`, `5 × (5 − 1/5)` | ✅ | same value, same numbers |
   | `24` | ❌ | not a formula |
   | `12+12`, `8*3`, `25-1` | ❌ | right value, **wrong numbers** |
   | `5 * (5 - 0.2)`, `5*4.8` | ❌ | turns `1/5` into a number that wasn't given |
   | `5*5-(5/5)` | ❌ | uses `5` four times; only three were given |

   - The same-numbers check is a **hard requirement**: reaching the total any other way doesn't
     solve the puzzle.
   - Rounding (rule 2) is what lets `8/(3-8/3)` count as 24 despite division leftovers.
   - A **plain-number** answer skips the check, so someone showing their working (`500*10` for
     `5000`) is still credited.
   - Broken input (`5*(5-`, `((((`, `1/0`) is rejected, never crashes.
8. **Blank answers are always rejected.**

> **Authoring guidance.** Store a **formula** only when the formula *is* the puzzle ("make 24 from
> these numbers"). When the answer is a value, store the **number** — `0.333`, not `1/3` — or
> solvers must rebuild a formula from the same numbers instead of just giving the value.

### Guarantees

- Ordinary words never become numbers (`banana`, or `thousand` on its own).
- The rules don't depend on order, and hold for both the stored answer and the submission.

## Non-goals

- **Accepting a half-simplified formula** (`5 * (5 - 0.2)`), sound arithmetic or not. The strict
  same-numbers check is the accepted cost of guaranteeing a "make 24 from these numbers" puzzle
  can only be solved with those numbers. The author can always list such forms with `;`.
- Reading the required numbers from the **question text** — they come from the stored answer,
  which is by definition a valid solution.
- Algebra beyond arithmetic — no variables, powers or roots.
- Forgiving misspellings (`kyboard` is wrong), or understanding prose answers.

## Acceptance criteria

Unit tests in `OneADay.Tests/` pin every rule, and every live teaser has its own accept and reject
cases:

- [x] `AnswerValidationTests.cs` — the rule matrix, including number words and equivalent formula
      forms
- [x] `TeaserBankTests.cs` — every question in the live bank, accepted phrasings and plausible
      wrong answers

> **History:** a running total of tests used to be a criterion here. It read 144 while the suite
> was at 284 — it changes whenever *anything* gains a test — and a number that drifts on its own
> looks verified when it isn't. The two files above are the criterion; run them.

> **History — regression:** rule 5 once split from the *first* `(` to the *last* `)`, so
> `5*(5-(1/5))` accepted a bare `5`. Rule 6 exists because of that bug; its tests must stay.

## Implementation notes

Matching is `BrainTeaser.AcceptsAnswer` and its private helpers (`Variants`, `Matches`,
`TryParseNumber`, `TryParseNumberWords`, `TrySplitNumberAndUnit`). The arithmetic, rounding
included, lives in `Models/Arithmetic.cs`, moved out when the Twenty Four game shipped so the two
share one copy ([PRD 07](07-twenty-four.md)) — two copies would drift, and rounding is exactly the
kind of detail that drifts silently.

It all runs in memory; a submission is never stored, logged, or inserted into any query.
`Arithmetic` is hand-written with a **cap on how deeply brackets nest**, so hostile input
(`((((((…`) is rejected instead of crashing the server. A very long *flat* expression never
nests, so callers cap length too: 300 characters on the daily challenge, 120 in the game.

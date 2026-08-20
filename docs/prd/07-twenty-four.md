# PRD 07 — The Twenty Four game

**Status:** Spec of record · **Route:** `/twentyfour` (also answers `/questions`)

## Problem

Removing the archive ([PRD 04](04-archive-and-discovery.md)) was right for the daily
habit, but it left a visitor who wanted more than one puzzle with nowhere to go, and
left the "Twenty Four" slot holding nothing but a maintenance notice.

The answer is a game that **generates its own puzzles**. Someone can play it all
afternoon without touching the teaser bank, so unlimited play here can't undermine
one-a-day over there.

## The game

Four numbers are dealt. Combine **all four**, each used exactly once, with
`+ - * /` and parentheses to make **24**.

- Numbers run **1–10 inclusive**, mirroring a deck of cards with face cards counting
  as 10 — so **duplicates are normal**, not a bug.
- Fractions are part of the game: the classic `3 3 8 8` is solvable only as
  `8 / (3 - 8/3)`.

> **The UI must not teach this.** No worked example, no "fractions are allowed" note,
> no example in the input placeholder, and the decimal-point error must not point at
> division as the workaround. Realising a hand needs a fraction is the best moment the
> game has, and handing it over cheapens every hand after it. Keep the discovery on
> the player's side.

## Requirements

### Dealing

1. Four numbers, each uniformly random in 1–10, duplicates allowed.
2. **Hands are dealt straight from the deck — never screened for solvability.**
   Some hands genuinely cannot make 24 (`1 1 1 1` never can), and that is the point:
   not knowing whether a hand is crackable is part of the challenge. Screening would
   also mean running the full search on every deal purely to discard hands.
3. The solver is therefore consulted **only when the player presses Pass**, never on
   the dealing path.

### Playing

3. An expression box, a **Submit** button, and a **Pass** button.
4. Submit is disabled while the box is empty; **Enter** submits.
5. **Pass** deals a fresh hand immediately and reveals **nothing** — no solution, no
   verdict on whether the hand was even possible. Handing over the answer to a puzzle
   the player could still crack defeats the game. Passing counts as a hand played but
   not solved.
6. After a **solve** the round locks (input and buttons disabled) and a **Deal a new
   hand** button appears. Passing needs no such step — it has already moved on.
7. A correct answer triggers the same confetti as the daily challenge.
8. A per-visit tally ("Solved 3 of 5 hands this visit") sits at the foot of the page.
   It is deliberately **not persisted** — this is a diversion, not a second streak to
   maintain.

### Judging a submission

Checked in this order, each failure naming what actually went wrong:

| # | Check | Example message |
|---|---|---|
| 1 | Not empty | *Type an expression first.* |
| 2 | **Only legal characters** — digits, `+ - * /`, parentheses, whitespace | *'^' isn't allowed. Use your numbers with + - * / and parentheses only.* |
| 3 | No decimal points | *Decimal points aren't allowed — use only the numbers you were dealt.* |
| 4 | **Parentheses balanced** — every `(` closed, in order | *Those parentheses don't match up.* |
| 5 | **Well-formed** arithmetic | *That isn't a complete expression…* |
| 6 | **Exactly the numbers dealt**, as a multiset | *Use each of your numbers exactly once. You were dealt 1, 2, 3, 9, but used 12, 12.* |
| 7 | **Evaluates to 24** | *That comes to 15, not 24.* |

- Rule 6 is what stops `24`, `12 + 12`, `38 + 5 - 8 - 8` (digits glued into a new
  number), or reusing a card more often than it was dealt.
- Rule 4 must reject right-count-wrong-order cases like `)3 + 5(`, not merely count
  brackets.
- **Rule 7 compares at the thousandths place**, because fraction play does not divide
  evenly: `8 / (3 - 8/3)` lands a hair off 24 in decimal arithmetic and must still
  count. This is the same rounding the teaser answers use — see
  [PRD 02](02-answer-evaluation.md).

## Non-goals

- Persisting scores, streaks, or leaderboards across visits
- A timer or any pressure mechanic
- Difficulty selection or curating which hands appear
- Revealing solutions anywhere in the game, including on Pass

## Acceptance criteria

- [x] Dealing does **not** screen for solvability (impossible hands still appear)
- [x] Dealing never invokes the solver (20k deals stay well under a second)
- [x] Pass on an impossible hand says so instead of showing nothing
- [x] Hands stay within 1–10 and duplicates occur
- [x] Illegal operators (`^ % ! sqrt`) and decimal points are rejected by name
- [x] Unbalanced parentheses rejected, including right-count-wrong-order
- [x] Expressions not using exactly the dealt numbers are rejected
- [x] `8 / (3 - 8/3)` is accepted for `3 3 8 8`; near-misses are not
- [x] A wrong total reports the value actually reached
- [x] Pass reveals nothing and moves straight to a new hand
- [x] Correct answers fire confetti and lock the round
- [x] Nothing in the UI or the error messages reveals that fractions are viable

## Implementation notes

`Models/TwentyFourGame.cs` holds dealing, the solver, and `Check`.
`Components/Pages/TwentyFour.razor` is the UI.

The expression evaluator was **extracted out of `BrainTeaser` into
`Models/Arithmetic.cs`** so the game and the teaser answer-matching share one
implementation — including the thousandths rounding. Duplicating it would have let
the two drift apart, and the fraction rule is exactly the kind of subtlety that would
drift silently.

> The solver (`FindSolution`/`HasSolution`) is **not on any player-facing path** —
> dealing doesn't screen and Pass doesn't reveal. It survives as a test oracle: every
> solution it finds is fed back through `Check`, so the two cannot disagree about what
> a valid answer looks like. If it ever gets wired into the UI, re-read requirement 5
> first.

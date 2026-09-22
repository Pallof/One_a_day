# PRD 07 — The Twenty Four game

**Status:** Spec of record · **Route:** `/twentyfour` (also answers `/questions`)

## In plain terms

You're dealt four numbers. Combine all four — each used exactly once — with `+ − × ÷` and
brackets to make **24**. It makes up its own puzzles, so someone can play all afternoon
without using up the hand-written daily teasers.

The design rule running through all of it: **the game never helps.** It doesn't check whether
a hand is even possible, it never shows a solution, and it must never hint that fractions are
allowed. Working that out is the best moment the game has.

## Problem

Removing the archive ([PRD 04](04-archive-and-discovery.md)) was right for the daily habit, but
it left a visitor who wanted more than one puzzle with nowhere to go, and left the "24" slot
holding nothing but a maintenance notice. A game that **generates its own puzzles** answers
both: unlimited play here can't undermine one-a-day over there.

## The game

Four numbers, combine all four to make 24.

- Numbers run **1–10**, mirroring a deck with face cards as 10 — so **duplicates are normal**,
  not a bug.
- Fractions are part of the game: the classic `3 3 8 8` is solvable only through one.

> **The UI must not teach this.** No "fractions are allowed" note, and the decimal-point error
> must not point at division as the workaround. Realising a hand needs a fraction is the best
> moment the game has, and handing it over cheapens every hand after it.

### Examples must be easy base cases

The page carries exactly one: *dealt 6 6 6 6, your answer could be `6 + 6 + 6 + 6`* — the
easiest hand possible, addition only, no brackets. It shows what to type and reveals no method.

Two tests before adding or changing an example:

1. **Is it an easy base case?** Anything revealing a *method* is banned — that division makes
   fractions, that brackets can build one, that an impossible-looking hand has a trick.
2. **Is this hand in the teaser bank?** The two surfaces share the same puzzle space, so a hand
   that's fine today becomes a spoiler the moment it's authored as a challenge.

> **Why the second test exists.** The page originally worked through `3 3 8 8` and its only
> solution. That hand **is a teaser in the live bank** (Hard, 2026-07-27), so the example was
> permanently publishing the answer to an actual Challenge of the day on a different page —
> and it was the hardest well-known hand in the game, handing over the technique wholesale.
> `6+6+6+6` passes both tests. Don't "improve" it into something clever.

## Requirements

### Dealing

1. Four numbers, each uniformly random in 1–10, duplicates allowed.
2. **Hands are never screened for solvability.** Some genuinely can't make 24 (`1 1 1 1` never
   can), and that's the point — not knowing whether a hand is crackable is part of the
   challenge. Screening would also mean running the full search on every deal just to discard
   hands.
3. **The solver is never consulted on any player-facing path** — not when dealing, not when the
   player passes. Nothing the player can do makes the game reveal whether a hand was possible.

### Playing

4. An expression box, **Submit**, and **Pass**. Submit is disabled while the box is empty;
   Enter submits.
5. <a id="pass-rule"></a>**Pass** deals a fresh hand and reveals **nothing** — no solution, and
   no verdict on whether the hand was even possible. Handing over an answer the player could
   still find defeats the game, and saying a hand was impossible is its own spoiler: it
   retroactively tells them their time was wasted. Passing counts as played, not solved.
6. After a **solve** the round locks and a **Deal a new hand** button appears. Passing needs no
   such step — it has already moved on.
7. A correct answer fires the same confetti as the daily challenge.
8. A per-visit tally ("Solved 3 of 5 hands this visit") sits at the foot of the page,
   deliberately **not saved** — this is a diversion, not a second streak to maintain.

### Showing the hand

9. The four numbers render as **playing cards** — corner ranks, a centre pip, red/black suits —
   because the hand *is* a deal.
10. **Suits are cosmetic, assigned by position.** The game has no suits and nothing reads them.
    They exist so **duplicates are tellable apart**, which is load-bearing for the next rule:
    with a pair of 9s, "one of them is dimmed" is ambiguous, while "9♠ is dimmed and 9♣ is not"
    isn't.
11. <a id="spent-cards"></a>A card **dims once its number appears in the expression** and
    **lights back up the moment it's deleted**. Duplicates are spent left to right, so typing
    one 9 dims the first 9 only.

> **Why dimming doesn't break the no-help rule.** The line is **bookkeeping versus insight.**
> A dimmed card tells you what you already typed. It says nothing about how to reach 24 — not
> whether the hand is solvable, not which operator to try, not that a fraction is needed. It
> removes clerical work (*"have I used both 8s?"*) that is tedious rather than interesting.
>
> Keep the distinction for future additions. "Highlight the numbers you haven't used" is
> bookkeeping. "Grey out operators that can't reach 24" is insight, and belongs nowhere near
> this page.

Which cards are spent is **recomputed from the text on every render**, not stored, so it can't
fall out of sync with what's typed. It uses the same parser that validates submissions, so the
cards can never disagree with what the game accepts — `1` and `10` are distinguished correctly,
and a number not in the hand dims nothing.

### Judging a submission

Checked in this order, each failure naming what actually went wrong:

| # | Check | Example message |
|---|---|---|
| 1 | Not empty | *Type an expression first.* |
| 2 | **Only legal characters** | *'^' isn't allowed. Use your numbers with + - * / and parentheses only.* |
| 3 | No decimal points | *Decimal points aren't allowed — use only the numbers you were dealt.* |
| 4 | **Brackets balanced**, in order | *Those parentheses don't match up.* |
| 5 | **Well-formed** arithmetic | *That isn't a complete expression…* |
| 6 | **Exactly the numbers dealt**, duplicates counted | *Use each of your numbers exactly once. You were dealt 1, 2, 3, 9, but used 12, 12.* |
| 7 | **Evaluates to 24** | *That comes to 15, not 24.* |

- Check 6 is what stops `24`, `12 + 12`, or `38 + 5 - 8 - 8` (digits glued into a new number).
- Check 4 must reject right-count-wrong-order cases like `)3 + 5(`, not merely count brackets.
- **Check 7 compares to three decimal places**, because fraction play doesn't divide evenly:
  `8 / (3 - 8/3)` lands a hair off 24 and must still count. Same rounding as the teaser answers
  ([PRD 02](02-answer-evaluation.md)).

## Non-goals

- Persisting scores, streaks or leaderboards across visits
- A timer or any pressure mechanic
- Difficulty selection or curating which hands appear
- Revealing solutions anywhere, including on Pass

## Acceptance criteria

- [x] Dealing does **not** screen for solvability — impossible hands still appear
- [x] Dealing never invokes the solver (20k deals stay well under a second)
- [x] Hands stay within 1–10 and duplicates occur
- [x] Illegal operators and decimal points are rejected by name
- [x] Unbalanced brackets rejected, including right-count-wrong-order
- [x] Expressions not using exactly the dealt numbers are rejected
- [x] `8 / (3 - 8/3)` is accepted for `3 3 8 8`; near-misses are not
- [x] A wrong total reports the value actually reached
- [x] Pass reveals nothing and moves straight to a new hand
- [x] The solver is unreachable from the UI — no player action invokes it
- [x] Cards dim as their number is typed and **relight when erased**
- [x] Duplicates spend left to right; one 9 typed dims one 9, not both
- [x] `1` and `10` dim the right card; a number not in the hand dims none
- [x] A solved hand shows **all four cards lit** — dimming the whole hand under the confetti
      reads as a loss
- [x] Correct answers fire confetti and lock the round
- [x] Nothing in the UI or the error messages reveals that fractions are viable
- [x] The one on-page example is an easy base case, and uses no hand from the teaser bank

## Implementation notes

`Models/TwentyFourGame.cs` holds dealing, the solver and `Check`.
`Components/Pages/TwentyFour.razor` is the UI.

The expression evaluator was **extracted into `Models/Arithmetic.cs`** so the game and the
teaser answer-matching share one implementation, including the thousandths rounding.
Duplicating it would have let the two drift, and the fraction rule is exactly the kind of
subtlety that drifts silently.

> The solver is **not on any player-facing path.** It survives purely as a test oracle: every
> solution it finds is fed back through `Check`, so the two can't disagree about what a valid
> answer looks like. If it's ever wired into the UI, re-read [the Pass rule](#pass-rule) first
> — an earlier draft had it running on Pass, and that's exactly what this note prevents.

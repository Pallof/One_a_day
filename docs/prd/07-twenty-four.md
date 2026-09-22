# PRD 07 — The Twenty Four game

**Status:** Spec of record · **Route:** `/twentyfour` (also answers `/questions`)

## In plain terms

You're dealt four numbers. Combine all four — each exactly once — with `+ − × ÷` and brackets to
make **24**.

Removing the archive ([PRD 04](04-archive-and-discovery.md)) was right for the daily habit, but it
left visitors who wanted more than one puzzle with nowhere to go, and the "24" slot holding only a
maintenance notice. A game that **makes up its own puzzles** answers both: unlimited play here can't
use up the hand-written daily teasers.

The rule running through it all: **the game never helps.** It doesn't check whether a hand is
possible, never shows a solution, and never hints that fractions are allowed.

## The game

Numbers run **1–10**, like a deck of cards with face cards counting 10 — so **duplicates are
normal**, not a bug. Fractions are part of the game. The easiest case: dealt 1 2 2 6,
`6 / (1/2) * 2` makes 24, because dividing by a half doubles. A few harder hands can only be
solved with a fraction.

> **The page must not teach this.** No "fractions are allowed" note, and the decimal-point error must
> not point to division as the way round it. Realising a hand needs a fraction is the best moment
> the game has; handing it over cheapens every hand after.

### The example must be an easy base case

The page has one example: *dealt 6 6 6 6, your answer could be `6 + 6 + 6 + 6`* — the easiest hand
there is. It shows what to type and reveals no method. Before adding or changing an example:

1. **Is it an easy base case?** Nothing that reveals a *method* — that division makes fractions,
   that brackets can build one, that an impossible-looking hand has a trick.
2. **Is the hand in the teaser bank?** The game and the teasers share the same puzzles, so a hand
   that's fine today becomes a spoiler the moment it's written up as a challenge.

> **History — why the second test exists:** the page's first example used a hand that is also a
> teaser in the bank, so it permanently published the answer to a real challenge of the day.
> Don't "improve" `6+6+6+6` into something clever.

## Requirements

### Dealing

1. Four numbers, each equally likely from 1 to 10, duplicates allowed.
2. **Hands are never screened for solvability.** Some can't make 24 (`1 1 1 1` never can), and
   that's the point — not knowing whether a hand can be cracked is part of the challenge. Screening
   would also mean solving every hand just to throw some away.
3. **The built-in solver is never used by anything the player does** — not dealing, not passing — so
   nothing reveals whether a hand was possible.

### Playing

4. An answer box, **Submit** and **Pass**. Submit is disabled while the box is empty; Enter submits.
5. <a id="pass-rule"></a>**Pass** deals a fresh hand and reveals **nothing** — no solution, and no
   verdict on whether the hand was possible. Handing over an answer the player could still find
   defeats the game, and saying a hand was impossible is its own spoiler: it tells them their time
   was wasted. Passing counts as played, not solved.
6. After a **solve** the round locks and a **Deal a new hand** button appears. Pass needs no such
   step — it has already moved on.
7. A correct answer fires the daily challenge's confetti.
8. A tally for the visit ("Solved 3 of 5 hands this visit") sits at the foot of the page,
   deliberately **not saved** — a diversion, not a second streak to keep up.

### Showing the hand

9. The numbers are drawn as **playing cards** — corner ranks, a centre symbol, red and black
   suits — because the hand *is* a deal.
10. **Suits are decoration, assigned by position**; nothing reads them. They let **duplicates be
    told apart**, which the next rule needs: with a pair of 9s, "one is dimmed" is ambiguous, while
    "9♠ is dimmed and 9♣ isn't" is not.
11. <a id="spent-cards"></a>A card **dims once its number is typed** and **lights up again the moment
    it's deleted**. Duplicates are used left to right: typing one 9 dims the first 9 only.

> **Why dimming doesn't break the no-help rule:** it's **bookkeeping, not insight.** A dimmed card
> tells you what you've typed — nothing about how to reach 24, whether the hand is solvable, which
> operator to try, or that a fraction is needed. It saves clerical work (*"have I used both 8s?"*),
> not thinking. Hold that line: "highlight the numbers you haven't used" is bookkeeping; "grey out
> operators that can't reach 24" is insight, and belongs nowhere near this page.

Which cards are used is **worked out afresh from the typed text every time**, never stored, so it
can't fall out of step. It reads the text exactly as the answer check does, so the cards never
disagree with what the game accepts — `1` and `10` are told apart, and a number not in the hand
dims nothing.

### Judging an answer

Checked in this order, each failure naming what went wrong:

| # | Check | Example message |
|---|---|---|
| 1 | Not empty (and at most 120 characters) | *Type an answer first.* |
| 2 | **Only allowed characters** | *'^' isn't allowed. Use your numbers with + - * / and parentheses only.* |
| 3 | No decimal points | *Decimal points aren't allowed — use only the numbers you were dealt.* |
| 4 | **Brackets balanced**, in order | *Those parentheses don't match up.* |
| 5 | **Complete arithmetic** | *That isn't a complete answer — check for a missing number or a stray operator.* |
| 6 | **Exactly the numbers dealt**, duplicates counted | *Use each of your numbers exactly once. You were dealt 1, 2, 3, 9, but used 12, 12.* |
| 7 | **Comes to 24** | *That comes to 15, not 24.* |

- Check 6 stops `24`, `12 + 12`, and `38 + 5 - 8 - 8` (digits glued into a new number).
- Check 4 must reject right-count-wrong-order cases like `)3 + 5(`, not just count brackets.
- **Check 7 compares to three decimal places**, because fractions don't always divide evenly: an
  answer that goes through one can land a hair off 24 and must still count — the same rounding
  teaser answers use ([PRD 02](02-answer-evaluation.md)).

## Non-goals

- Saving scores, streaks or leaderboards between visits
- A timer, or any other pressure
- Choosing a difficulty, or curating which hands appear
- Revealing solutions anywhere, including on Pass

## Acceptance criteria

- [x] Dealing does **not** screen for solvability — impossible hands still appear — and never
      invokes the solver (20k deals stay well under a second)
- [x] Hands stay within 1–10 and duplicates occur
- [x] Illegal operators and decimal points are rejected by name; unbalanced brackets are rejected,
      including right-count-wrong-order
- [x] Expressions not using exactly the dealt numbers are rejected
- [x] An answer that goes through a fraction and lands a hair off 24 is accepted; near-misses are
      not
- [x] A wrong total reports the value actually reached
- [x] Pass reveals nothing and moves straight to a new hand
- [x] The solver is unreachable from the UI — no player action invokes it
- [x] Cards dim as their number is typed and **relight when erased**; duplicates spend left to
      right (one 9 typed dims one 9, not both)
- [x] `1` and `10` dim the right card; a number not in the hand dims none
- [x] A solved hand shows **all four cards lit** — dimming the whole hand under the confetti reads
      as a loss
- [x] Correct answers fire confetti and lock the round
- [x] Nothing in the UI or the error messages reveals that fractions are viable
- [x] The one on-page example is an easy base case, and uses no hand from the teaser bank

## Implementation notes

`Models/TwentyFourGame.cs` holds dealing, the solver and `Check`; `Components/Pages/TwentyFour.razor`
is the page. The arithmetic and its rounding are shared with teaser answers in
`Models/Arithmetic.cs` ([PRD 02](02-answer-evaluation.md) says why).

> The solver is **not used by anything the player does.** It survives only as a test check: every
> solution it finds is fed back through `Check`, so the two can't disagree about what a valid answer
> is. If it's ever wired into the page, re-read [the Pass rule](#pass-rule) first — an earlier draft
> ran it on Pass, which is exactly what this note prevents.

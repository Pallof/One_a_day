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
   **The rules say so up front:** *"Not every hand can make 24. If you're stuck, Pass deals a fresh
   one."* Players took every hand to be possible and got frustrated *(player feedback,
   2026-10-07)*. It's a fact about the deck, never about the hand on the table, so it gives
   nothing away. Never say which hands, or how many, or anything about how the hard ones go.
3. **The built-in solver is never used by anything the player does** — not dealing, not passing — so
   nothing reveals whether a hand was possible.

### Playing

4. An answer box, **Submit** and **Pass**. Submit is disabled while the box is empty; Enter submits.
5. <a id="pass-rule"></a>**Pass** deals a fresh hand and reveals **nothing** — no solution, and no
   verdict on whether the hand was possible. Handing over an answer the player could still find
   defeats the game, and saying a hand was impossible is its own spoiler: it tells them their time
   was wasted. Passing counts as played, not solved.
6. After a **solve** the round locks and a **Deal a new hand** button appears. Pass needs no such
   step — it has already moved on. The lock holds **on the server too**: a Submit or Pass sent
   over the live connection after a solve is ignored, so it can't inflate the tally or the site
   metrics ([PRD 16](16-site-metrics.md); until 2026-10-06 only the browser's buttons enforced it).
7. A correct answer fires the daily challenge's confetti.
8. A tally for the visit ("Solved 3 of 5 hands this visit") sits at the foot of the page,
   deliberately **not saved** — a diversion, not a second streak to keep up.
   *Report an issue* (the ☰ menu, or the floating button on a wide screen) records the hand on
   screen, so an "answer not accepted" report can be checked ([PRD 06](06-community-feedback.md)).

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

### The keypad, on touch screens

Typing `(10 − 4) × 2` on a phone means swapping between the letter, number and symbol keyboards
for nearly every key *(visitor feedback, 2026-10-07)*. So under the answer box there are buttons:

```
[ 10 ][  4 ][  2 ][  2 ]     the hand, in the order dealt
[  + ][  − ][  × ][  ÷ ]
[  ( ][  ) ][    ⌫     ]
```

12. **Touch screens only.** It's hidden where the device reports a mouse, and so has a keyboard;
    a device that reports neither gets the keypad. The *"or press the space bar"* line under
    **Deal a new hand** follows the same rule the other way round — a phone has no space bar.
13. **A tap adds to the end of the answer, spaced as it would be written**: no space after `(`
    or before `)`, one everywhere else. Two numbers tapped in a row stay apart — `3 8`, not
    `38` — so the check names the real mistake (a missing symbol) rather than a number the
    player never tapped. The symbols are the rules' own `− × ÷`, which the check reads as
    `- * /`. Nothing is added past the 120-character limit.
14. **⌫ takes back one tap**: the last number or symbol, so a `10` goes in one press.
15. **A number button goes out with its card**: it's disabled while its card is dimmed and comes
    back with it, from the same reading of the text — so it follows typing too. That makes
    duplicates spend left to right here as well: tapping the second 3 can fade the first 3's
    button. Harmless, as the two are the same number; for that reason the buttons are plain ink,
    not the cards' suit colours, which would make the swap look like a mistake.
16. **It only types.** Every answer goes through the same check, and the keypad knows nothing
    about which symbols could help — the same line as the cards: bookkeeping, not insight. Once a
    hand is solved every button is disabled, and a tap or ⌫ sent over the live connection anyway
    changes nothing.
17. **Tapping a button never focuses the answer box**, which would raise the phone's keyboard over
    the keypad. The box still takes typing for anyone who wants it.

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
- [x] Hands are four cards within 1–10, every value turns up (10 included), and duplicates occur —
      checked against the rule itself rather than the code's own constants
- [x] Illegal operators and decimal points are rejected by name; unbalanced brackets are rejected,
      including right-count-wrong-order; incomplete expressions are reported as malformed, exactly
- [x] Expressions not using exactly the dealt numbers are rejected, counting each card: a number
      used twice when dealt once fails even when every number used was dealt
- [x] An answer at exactly the 120-character limit is judged; one character more is too long
- [x] An answer that goes through a fraction and lands a hair off 24 is accepted; near-misses are
      not — 24.1 included, about the closest four cards can come
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
- [x] The rules say not every hand can make 24, and never mention fractions —
      `TwentyFourPageTests`, both halves mutation-verified

### The keypad — `TwentyFourPageTests`, each mutation-verified

- [x] The number buttons are the hand in the order dealt, then the six symbols and ⌫
- [x] Tapped answers read as written — `(10 − 4) × 2 × 2`, `8 × 3 ÷ 2 × 2` — and are judged
      correct like typed ones
- [x] Two numbers tapped in a row stay apart, and the check calls it incomplete
- [x] ⌫ takes back one tap — `10` in one press — and is disabled on an empty box
- [x] A number button goes out with its card and comes back on ⌫; duplicates spend left to right;
      typed text disables them too
- [x] After a solve every button is disabled, and a forced tap or ⌫ leaves the answer alone
- [x] A tap that would pass 120 characters adds nothing; the one that reaches exactly 120 is kept

**By hand, in a phone-sized browser with touch:** the keypad shows and the space-bar line doesn't;
with a mouse it's the other way round; tapping keys never brings up the keyboard; quick double
taps don't zoom the page.

## Implementation notes

`Models/TwentyFourGame.cs` holds dealing, the solver and `Check`; `Components/Pages/TwentyFour.razor`
is the page. The arithmetic and its rounding are shared with teaser answers in
`Models/Arithmetic.cs` ([PRD 02](02-answer-evaluation.md) says why).

> The solver is **not used by anything the player does.** It survives only as a test check: every
> solution it finds is fed back through `Check`, so the two can't disagree about what a valid answer
> is. If it's ever wired into the page, re-read [the Pass rule](#pass-rule) first — an earlier draft
> ran it on Pass, which is exactly what this note prevents.

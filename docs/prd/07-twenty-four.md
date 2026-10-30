# PRD 07 — The Twenty Four game

**Status:** Spec of record (Impossible and the board added 2026-10-29) · **Route:** `/twentyfour` (also answers `/questions`)

## In plain terms

You're dealt four numbers. Combine all four — each exactly once — with `+ − × ÷` and brackets to
make **24**.

Removing the archive ([PRD 04](04-archive-and-discovery.md)) was right for the daily habit, but it
left visitors who wanted more than one puzzle with nowhere to go, and the "24" slot holding only a
maintenance notice. A game that **makes up its own puzzles** answers both: unlimited play here can't
use up the hand-written daily teasers.

The rule running through it all: **the game never shows how.** It never shows a solution and never
hints that fractions are allowed. Whether a hand *can* be done it tells only a player who calls it
**Impossible**, staking their run on the call.

To give players something to chase, hands solved in a row make a **run**, and the longest runs go on
a **scoreboard**, today's and of all time *(author's decision, 2026-10-29)*.

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
   that's the point — not knowing whether a hand can be cracked is part of the challenge, and the
   Impossible button needs impossible hands to be dealt. About one dealt hand in seven can't make
   24 (1,442 of the 10,000 possible deals; 149 of the 715 different hands).
   **The rules say so up front:** *"Not every hand can make 24. If you think yours can't, press
   Impossible."* Players took every hand to be possible and got frustrated *(player feedback,
   2026-10-07)*. It's a fact about the deck, never about the hand on the table, so it gives
   nothing away. Never say which hands, or how many, or anything about how the hard ones go.
3. **The solver answers one question, and only for Impossible:** whether the hand can make 24.
   Dealing and Pass never ask it, and nothing ever shows a solution it found.

### Playing

4. An answer box, **Submit**, **Impossible** and **Pass**. Submit is disabled while the box is empty;
   Enter submits, on any hand still in play (until 2026-10-29 it did nothing on a hand Pass had dealt
   until Submit was clicked once).
5. <a id="pass-rule"></a>**Pass** deals a fresh hand and reveals **nothing** — no solution, and no
   verdict on whether the hand was possible. Handing over an answer the player could still find
   defeats the game, and a verdict is what Impossible is for, at the risk of the run. Passing
   counts as played, not solved, and **ends the run**.
6. A wrong answer says what went wrong (see *Judging an answer*) and leaves the run alone: try again
   as often as you like.
7. After a **solve** or an **Impossible call** the round is over: the hand stays on screen with
   every card lit, the box and buttons lock, and a **Deal a new hand** button appears (or press the
   space bar, except in a box being typed in or on a focused button, which space presses). Pass
   needs no such step — it has already moved on. The lock holds **on the server too**: a Submit,
   Impossible, Pass or keypad tap sent over the live connection after the round is over is
   ignored, so it can't inflate the run, the board or the site metrics
   ([PRD 16](16-site-metrics.md); until 2026-10-06 only the browser's buttons enforced it).
   The other way round too: **Deal a new hand** sent while a hand is in play is ignored. Blazor
   keeps a removed button's click alive until the browser confirms the redraw, so a quick double
   tap, or a client sending the click again, would otherwise deal mid-hand: a free Pass that keeps
   the run (found in review, 2026-10-29).
8. A correct answer, and a right Impossible call, fire the daily challenge's confetti.
   *Report an issue* (the ☰ menu, or the floating button on a wide screen) records the hand on
   screen, so an "answer not accepted" report can be checked ([PRD 06](06-community-feedback.md)).

### Impossible

9. **Impossible** says the player thinks the hand can't make 24. It can be pressed at any point in
   the round, with or without an answer typed.
   - **Right** (the hand can't make 24): it counts like a solve and adds one to the run.
     *"Right, these four can't make 24. That's 4 in a row."*
   - **Wrong** (the hand can): the run ends. *"It could be done. Your run of 3 ends here."*
10. **A wrong call says only that the hand could be done, never how** *(author's decision,
    2026-10-29)*. The round is over, so an answer couldn't spoil this hand, but it would teach the
    method — often a fraction — and with it every hand after. The player can still look at the
    cards and work it out for themselves.
11. Without the penalty, Impossible would be a free Pass that sometimes scores, and nobody would
    ever press Pass. So a wrong call costs the run, as Pass does.
12. The solver works out each different hand's verdict once and remembers it. Ruling a hand out is
    its slowest case (about 20 ms, as it tries everything), and there are only 715 different hands,
    so remembering bounds the work however often anyone presses the button.

### The run and the board

13. **A run** is hands in a row solved or rightly called impossible. It's shown under the cards
    (*"Current run: 3"*) and in each correct answer's message (*"That's 3 in a row."*; the first is
    *"That starts a run."*). Pass and a wrong Impossible call end it, saying how long it was; a
    wrong answer doesn't.
14. **A run lasts the visit.** Leaving or reloading the page ends it, the same as Pass, so a reload
    can't be used to escape a hard hand.
15. **The board**, titled **High scores** *(author's decision, 2026-10-30; it was "Longest runs")*,
    lists the ten longest runs **today** (from midnight Pacific, like the daily teaser) and of
    **all time**. One row per player, holding their best; longest first, and of
    equal runs, whoever got there first. A run goes on the board as it grows, so a player who
    leaves mid-run keeps the place they'd reached. A run that spans midnight counts in full on the
    new day's list. **It folds to its title** *(author's decision, 2026-10-30)*, for a player who'd
    rather not see it while they play, and opens the same way. Open until folded; the browser
    remembers which way it was left, in `localStorage` (a preference, nothing the server needs).
    It's beside or under the game, so folding it once the page is live moves nothing in play.
16. **Players can't choose a name, and no two share one** *(author's decisions, 2026-10-29)*. The
    first time a browser gets a hand right, the server makes it a player: a random id, and **the
    next number in line**, so the first player is number 1, the next number 2, and so on. Its name
    is an animal picked from the id and its number, such as *Otter 17*. No two players ever share
    a number, so no two share a name. Opening the page, passing or a wrong call gives no number,
    so the numbers count people who've scored. The browser keeps its player, id and number
    together, encrypted by the server, so nobody can pick an id or a number, and with it someone
    else's name or row. Nothing a visitor types ever reaches another visitor's screen, so the
    board never needs moderating. Before their first hand right the page says *"Get a hand right
    and you'll get a name on the board"*; after, *"You play as Otter 17"*, and it marks the
    player's own rows *(you)*.
    - The costs: a name shows roughly how many have played before, and early players get small
      numbers. Until this, names were an animal and a four-digit number both made from the id,
      and could repeat.
17. **What's stored:** `App_Data/twentyfour-board.json` on the live site holds the twenty rows:
    each player's id, number and name, their run, and when they reached it; no address, nothing
    about who they are. The name is saved so the file can be read by eye.
    `App_Data/twentyfour-players.txt` holds the last number given out. It's saved before a number
    is given, and **a number that can't be saved isn't given**: that browser plays on off the
    board, as a restart could otherwise give the number again. A counter that's missing,
    unreadable or put back from an older backup carries on from the highest number on the board,
    so nobody on show is ever matched; a number given since then to someone off the board could
    come round again, and the site logs an error when the file can't be read. The player has a
    storage key of its own, not the suggestion form's visitor id, so the board can't be matched to
    suggestions. A browser that can't be read, or won't keep the player it's given, still plays and
    counts its run, but stays off the board: otherwise each of its visits would go on under a new
    name. It's asked once a visit, so it uses up one number at most. A board that can't be saved (a
    full disk, say) logs a warning and keeps its runs in memory until the next restart. A board
    file that can't be read is set aside as `twentyfour-board.unreadable-<time>.json`, logged as
    an error, and the board starts empty: better than the game down for everyone.

### Showing the hand

18. The numbers are drawn as **playing cards** — corner ranks, a centre symbol, red and black
   suits — because the hand *is* a deal.
19. **Suits are decoration, assigned by position**; nothing reads them. They let **duplicates be
    told apart**, which the next rule needs: with a pair of 9s, "one is dimmed" is ambiguous, while
    "9♠ is dimmed and 9♣ isn't" is not.
20. <a id="spent-cards"></a>A card **dims once its number is typed** and **lights up again the moment
    it's deleted**. Duplicates are used left to right: typing one 9 dims the first 9 only.

> **Why dimming doesn't break the never-shows-how rule:** it's **bookkeeping, not insight.** A dimmed card
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

21. **Touch screens only.** It's hidden where the device reports a mouse, and so has a keyboard;
    a device that reports neither gets the keypad. The *"or press the space bar"* line under
    **Deal a new hand** follows the same rule the other way round — a phone has no space bar.
22. **A tap adds to the end of the answer, spaced as it would be written**: no space after `(`
    or before `)`, one everywhere else. Two numbers tapped in a row stay apart — `3 8`, not
    `38` — so the check names the real mistake (a missing symbol) rather than a number the
    player never tapped. The symbols are the rules' own `− × ÷`, which the check reads as
    `- * /`. Nothing is added past the 120-character limit.
23. **⌫ takes back one tap**: the last number or symbol, so a `10` goes in one press.
24. **A number button goes out with its card**: it's disabled while its card is dimmed and comes
    back with it, from the same reading of the text — so it follows typing too. That makes
    duplicates spend left to right here as well: tapping the second 3 can fade the first 3's
    button. Harmless, as the two are the same number; for that reason the buttons are plain ink,
    not the cards' suit colours, which would make the swap look like a mistake.
25. **It only types.** Every answer goes through the same check, and the keypad knows nothing
    about which symbols could help — the same line as the cards: bookkeeping, not insight. Once a
    hand is solved every button is disabled, and a tap or ⌫ sent over the live connection anyway
    changes nothing.
26. **Tapping a button never focuses the answer box**, which would raise the phone's keyboard over
    the keypad. The box still takes typing for anyone who wants it.

### On a laptop

On a 14-inch laptop the page was 1,105px tall in an 860px window, and up to 1,634px with a full
board after a hand. Moving the board beside the game wasn't enough on its own: the narrower
column made the rules wrap onto more lines, and the answer box fell below the fold. So
*(author's decision, 2026-10-30)*:

27. **From 1100px wide, two columns**: the game on the left, the board on the right, Today beside
    All time. Narrower than that, on phones and tablets, it's the single column as before.
28. **The rules fold under "How to play"**, except the first line and "Not every hand can make
    24", which always show (the second came from players' frustration, so it never folds).
    "Show less" folds them again. Each visit starts folded.
29. **A finished hand is dealt from the answer box**, in place of Submit, Impossible and Pass,
    with "or press the space bar" beside it. A phone keeps its own box under the answer box.

[PRD 09](09-visual-design.md), "Fitting on one screen", has the measurements.

### Judging an answer

Checked in this order, each failure naming what went wrong:

| # | Check | Example message |
|---|---|---|
| 1 | Not empty (and at most 120 characters) | *Type an answer first.* |
| 2 | **Only allowed characters** | *'^' isn't allowed. Use your numbers with + - * / and parentheses only.* |
| 3 | No decimal points | *Decimal points aren't allowed. Use only the numbers you were dealt.* |
| 4 | **Brackets balanced**, in order | *Those parentheses don't match up.* |
| 5 | **Complete arithmetic** | *That isn't a complete answer. Check for a missing number or a stray operator.* |
| 6 | **Exactly the numbers dealt**, duplicates counted | *Use each of your numbers exactly once. You were dealt 1, 2, 3, 9, but used 12, 12.* |
| 7 | **Comes to 24** | *That comes to 15, not 24.* |

- Check 6 stops `24`, `12 + 12`, and `38 + 5 - 8 - 8` (digits glued into a new number).
- Check 4 must reject right-count-wrong-order cases like `)3 + 5(`, not just count brackets.
- **Check 7 compares to three decimal places**, because fractions don't always divide evenly: an
  answer that goes through one can land a hair off 24 and must still count — the same rounding
  teaser answers use ([PRD 02](02-answer-evaluation.md)).

## Non-goals

- A run that carries over between visits: the board keeps a player's best, never a run in progress
- Choosing your own name on the board
- A timer, or any other pressure
- Choosing a difficulty, or curating which hands appear
- Revealing solutions anywhere, including on Pass and a wrong Impossible call

## Risks

- **A script can play.** Twenty Four is easy for a program to solve, so a script driving a browser
  could put any run it likes on the board, and nothing here stops it. That's a problem for when
  someone actually does it *(author's decision, 2026-10-30)*. Until then, a row that shouldn't be
  there can be taken off by hand: stop the site from writing over the edit by restarting it
  straight after.
  - **A shadow ban was built and taken out (2026-10-29 to 30).** Three typed answers in a row
    under 2 seconds, timed on the server, kept a player off everyone else's board without telling
    them. A cold review found what to get right if it comes back:
    - **Count per run, typed answers only.** Counted across a whole visit, with one-click
      Impossible calls included, it caught strong players who pass hard hands and snap up easy
      ones. A Pass ends the run anyway, so a per-run count gives a script nothing.
    - **Make the hiding last as long as the ban.** The banned player's own rows were kept in
      memory, so every restart, including the one that applies a hand edit, showed them they were
      banned.
    - **Keep evidence and a way back.** The list held only ids; names and the answer times lived
      in a log that Fly doesn't keep for long, and an unban was undone at the player's next fast
      visit. A "cleared" list would make an unban stick.
    - **It's hidden only from people who don't look.** A private window shows the real board,
      and a script can check that way and start again under a new name. Consider hiding just the
      run rather than banning the player, or being open about the rule.
- **A wrong call is a hint.** "It could be done" tells the player the hand they gave up on was
  possible. That's the trade the author chose: the run is the price of finding out.

## Acceptance criteria

- [x] Dealing does **not** screen for solvability — impossible hands still appear — and never
      invokes the solver (20k deals stay well under a second)
- [x] The solver is right about all 715 different hands: 149 can't make 24, and every answer it
      finds for the other 566 passes the answer check
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
- [x] Cards dim as their number is typed and **relight when erased**; duplicates spend left to
      right (one 9 typed dims one 9, not both)
- [x] `1` and `10` dim the right card; a number not in the hand dims none
- [x] A solved or called hand shows **all four cards lit** — dimming the whole hand under the
      confetti reads as a loss
- [x] Correct answers fire confetti and lock the round
- [x] Nothing in the UI or the error messages reveals that fractions are viable
- [x] The one on-page example is an easy base case, and uses no hand from the teaser bank
- [x] The rules say not every hand can make 24, and never mention fractions —
      `TwentyFourPageTests`, both halves mutation-verified

### Impossible, the run and the board — each mutation-verified

- [x] A right call says so and adds one to the run; a wrong one says only "It could be done", ends
      the run, and shows no answer anywhere on the page
- [x] Pass ends the run and still says nothing about the hand; a wrong answer leaves the run alone
- [x] After a call the round is over on the server too: a forced Impossible, Pass or tap changes
      nothing, and the space bar deals as after a solve
- [x] Enter submits on a hand that Pass dealt
- [x] Each hand's verdict is worked out once, whatever order its cards are in, and a hand one card
      different gets its own
- [x] A new player gets the next number at their first hand right, not for opening the page,
      passing or a wrong call, and keeps it; a returning one keeps theirs and uses no number; one
      who scores before their browser answers gets a name once it does, and no new player is made
      while it's answering; a stored player with no number or no id counts as none; a browser that
      stores nothing, or won't keep its player, plays on off the board, using one number at most
- [x] The board: one row per player holding their best, longest first, ties to whoever got there
      first, ten rows, today's list empty from midnight Pacific, all time kept
- [x] The board names each player and marks the viewer's own row; an empty board says how to get on
- [x] The board survives a restart, isn't rewritten when nothing changed, stores each player's id,
      number and name and nothing else about them, and keeps counting in memory when it can't be
      saved
- [x] Numbers go in order and carry on after a restart; a missing, older or unreadable counter
      carries on past the highest number on the board, and an unreadable one is logged; a number
      that can't be saved isn't given out, nor used up
- [x] A name is the animal from the id and the number, and every animal turns up
- [x] Impossible calls count the player in the site metrics, right and wrong kept apart; the
      report adds them up and the weekly email lists them

### Review fixes, 2026-10-29 — each mutation-verified

- [x] Deal a new hand sent while a hand is in play deals nothing
- [x] A browser that reads but won't save its id plays on, off the board
- [x] An unreadable board file is set aside, logged, and the game goes on with an empty board
- [x] A remembered verdict belongs to all four cards: pairs one card apart, lowest or highest,
      keep their own

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

### The laptop layout — `TwentyFourPageTests` and `ChallengeLayoutTests`, each mutation-verified

- [x] Folded, the rules still show the first line and "Not every hand can make 24"; exactly the
      other three fold
- [x] How to play opens them and Show less folds them, with `aria-expanded` to match
- [x] Once a hand is over, the answer box holds Deal a new hand and the space-bar line, and marks
      the three buttons for hiding; the phone's box is still there; dealing from it starts a hand
- [x] The banner, the rules and the board are the page's own children, for the grid to place
- [x] The page widens for two columns at both its addresses, `/twentyfour` and `/questions`
- [x] The board folds to its title and opens again, with `aria-expanded` to match, and tells the
      browser each time; a browser that folded it last time starts folded; one whose storage
      fails starts open and still folds it

**By hand, in a phone-sized browser with touch:** the keypad shows and the space-bar line doesn't;
with a mouse it's the other way round; tapping keys never brings up the keyboard; quick double
taps don't zoom the page.

## Implementation notes

`Models/TwentyFourGame.cs` holds dealing, the solver and `Check`; `Components/Pages/TwentyFour.razor`
is the page. The arithmetic and its rounding are shared with teaser answers in
`Models/Arithmetic.cs` ([PRD 02](02-answer-evaluation.md) says why).

- **`TwentyFourGame.HasSolution`** is Impossible's verdict and the solver's only use on the page,
  remembered per hand. `FindSolution` stays off the page: the tests feed every solution it finds
  back through `Check`, so the two can't disagree about what a valid answer is. Before showing
  anything it finds, re-read [the Pass rule](#pass-rule) and requirement 10.
- **`Models/RunBoard.cs`** holds the board's rules, **`Services/TwentyFourBoard.cs`** the two lists
  and their files, numbers included, **`Models/PlayerName.cs`** the player and their name, and
  **`Services/TwentyFourPlayerStorage.cs`** the browser's player (`oad-24-player`). A browser
  holding an id from before numbers (2026-10-29) reads as having none.
- The board lives only on the live site: nothing copies it down or up, and admin doesn't show it.

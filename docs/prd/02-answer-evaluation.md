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

Some right answers are still too far from anything the author listed: "second place" when the
answer is "second", or "right behind the leader". So an answer the checker turns down can get a
**second opinion from Jev**, a yes/no decision model, where the question allows one *(author's
decision, 2026-10-27)*. See [The second opinion](#the-second-opinion-jev).

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
6. **Formulas are matched whole.** An answer with several or nested brackets — `5*(5-(1/5))` —
   is **not** split by rule 5, so a fragment such as `5` is rejected.
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
   | `5+5+1+5`, `5515` | ❌ | the formula's digits in order, but they don't make 24 |

   - The same-numbers check is a **hard requirement**: reaching the total any other way doesn't
     solve the puzzle.
   - **A formula is judged by this rule alone.** Rules 1 and 4 compare text with its symbols
     stripped, so reaching them would pass any string of the formula's digits in order: until the
     test audit of 2026-09-28, `5+5+1+5` (16) and a bare `5515` were accepted for the puzzle above,
     and both make-24 teasers in the bank had the hole.
   - Rounding (rule 2) is what lets a formula still count when its division doesn't come out
     even — the same reason a stored `0.333` accepts `1/3`.
   - A **plain-number** answer skips the check, so someone showing their working (`500*10` for
     `5000`) is still credited.
   - Broken input (`5*(5-`, `((((`, `1/0`, or a ratio part too big to read like `2:` and thirty
     nines) is rejected, never crashes.
8. **Blank answers are always rejected.**
9. **A ratio or a time is matched part by part:** `2:1` accepts `2 : 1`, but not `1:2`, and `3:15`
   doesn't accept `1:5`. Left to rule 1 it lost its colon, so `2:1` read as `21` and accepted a
   typed `21` *(found 2026-10-27, on the train-in-the-tunnel question)*.
10. **A lead-in before the answer is looked past.** If what was typed doesn't match, it's tried
    again without a lead-in at its start, one at a time: "the answer is", "my answer is",
    "answer:", "I think (that)", "I believe (that)", "I'd say", "it's", "it is". So
    `I think it's 9` passes for `9`. The rest still has to be an accepted answer on its own, so a
    lead-in never makes a wrong answer right. Only whole words come off, so `itself` keeps its
    `its`.

> **Authoring guidance.** Store a **formula** only when the formula *is* the puzzle ("make 24 from
> these numbers"). When the answer is a value, store the **number** — `0.333`, not `1/3` — or
> solvers must rebuild a formula from the same numbers instead of just giving the value.

### The second opinion: Jev

Jev is [TypeSafe's](https://openrouter.ai/blog/insights/what-is-jev/) decision model, reached
through OpenRouter. Asked a yes/no question, it answers with the chance of yes and nothing else,
so there's no text to parse and nothing it can be talked into saying.

**An answer is judged in this order** (`AnswerGrader`):

1. **The checker** (the rules above). If it says right, Jev is never asked.
2. **The author's rulings.** Each teaser has a *Ruled wrong* list, `;`-separated like its
   answers. An answer on it is wrong, whatever Jev said before.
3. **Whether Jev may be asked at all.** If not, the answer is wrong, and an earlier verdict on it
   isn't used either:
   - **It's switched on and has a key** (`Jev:Enabled`, and the secret `Jev:ApiKey`). Removing the
     key is the emergency switch, so it stops Jev's earlier yeses too, not just new questions.
   - **The question isn't a formula question.** The checker works formulas out (rule 7), so it's
     right by definition, and the make-24 puzzles stay as hard as they are.
   - **Where the accepted answers include a number, a number typed must be one of them.** "It's
     48" can go to Jev; "50" can't. Where every accepted answer is a number, the answer must give
     one. So Jev can never turn a wrong number into a right one. The cost: a right answer worded
     with another number ("one behind the leader") stays wrong without Jev being asked. On a
     question whose answers are all words, there's no number to hold a typed one to, so "1st
     place" is asked about, and Jev says no (0.04 to 0.06 in the trial).
   - **The answer has a letter or a digit.** "?", "..." or an emoji alone is wrong without asking.
4. **An earlier verdict.** Every answer Jev judges is kept, so the same answer to the same
   question, however it's typed, gets the same verdict without asking again. "However it's typed"
   means case, spacing and punctuation, but never the symbols or spaces inside a number: `2-1`,
   `2 1` and `21` are three answers, as are `2.5` and `25`. (Kept as one, Jev's yes to `2-1` once
   made a typed `21` right. PR 74 review.) What's kept is Jev's chance, not a yes or no, and it's
   held against today's bar: moving the bar regrades every kept answer at once, without asking
   Jev again. A verdict also records the question and accepted answers Jev saw. **Edit either and
   that question's verdicts no longer count**: each answer is asked about again the next time
   it's typed, and the new verdict replaces the old. Editing the picture, the solution, the date
   or the rulings changes nothing.
5. **Jev**, if there's room in today's caps, counted whether Jev answers or not and restarted with
   the app:
   - **300 questions a Pacific day** for the whole site, the ceiling on the bill.
   - **30 a day for each visitor address**, so one script can't spend everyone's. Thirty, not
     fewer, because a home or office network is one address (author's decision, 2026-10-28). An
     earlier verdict reused costs nothing, so a player typing normally uses one or two.

   Past either, the checker alone decides until midnight Pacific. At 10,000 kept verdicts, each
   new one pushes out the oldest, so a full file never switches Jev off.
6. **The answer counts as right if Jev's chance is 70% or more.** The chance is kept either way.
   Ninety was the first choice. On 2026-10-28, 47 trial answers to 17 questions, each sent twice,
   put every wrong one at 0.49 or less and every right one at 0.73 or more; ninety would have
   failed 8 of the 27 right ones, among them `a beach towel` and `right behind the leader`.
   If Jev doesn't answer within **5 seconds**, or fails, the answer is wrong and nothing is kept,
   so it's asked about again next time. The answer button reads "Checking…" while it waits.

**What Jev is sent:** the question, the author's accepted answers, and what was typed. No
address, no visitor id. (The address counts the visitor's share of the day, in memory only, and
is never sent, stored or logged.) The author agreed to this on 2026-10-27, knowing TypeSafe says requests
aren't used for training and keeps data "as long as reasonably necessary". What was typed can be
anything, which is why the request names asking to be marked correct as a "no".

**The author reviews Jev's verdicts in admin** ([PRD 05](05-authoring-and-admin.md)). The live
site keeps them in `judged-answers.json`; `deploy/pull-live-data.sh` copies it down
([PRD 10](10-admin-authentication.md)). Each verdict still in force and not yet dealt with has two
buttons:
- **Agree with Jev** clears the row and changes nothing else. Which rows were cleared is kept in
  `agreed-verdicts.json` on the author's Mac, which is never uploaded.
- **Rule it wrong**, shown only where the answer counts as right, adds what was typed to the
  teaser's *Ruled wrong* list in `teasers.json`, so publishing the bank puts it live. A `;` in it becomes a `,`, so it stays one ruling.

**Nothing a visitor typed becomes an accepted answer** (author's decision, 2026-10-28). The
accepted answers stay in the author's own words. To pass an answer Jev turned down, the author
adds it with Edit. (An *Add to accepted answers* button existed briefly: it took the visitor's
text literally, so a typed `second (i think)` would have made `i think` right on its own.)
Rulings never go back into the verdicts file, which keeps one writer per file.

**Writing more accepted answers keeps Jev rare.** On 2026-10-27 the bank gained 92 more, such as
`second place` and `16th day`. Tried on 123 typical answers, wrong verdicts went from 54 to one
(the `21` that rule 9 then fixed).

**Settings** (`Jev` in appsettings.json): `Enabled`, `Model` (pinned at `typesafe/jev-1.14`, so a
newer model can't quietly move the line), `Threshold` (0.7: author's decision, 2026-10-28, from the trial in step 6), `MaxPerDay` (300),
`MaxPerAddressPerDay` (30), `TimeoutSeconds`
(5: two was the first choice, raised so a slow network doesn't mark a right answer wrong;
author's decision, 2026-10-27), `Endpoint`. The key goes in user-secrets locally and `Jev__ApiKey` on the server
([PRD 11](11-deployment.md)).

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
- Forgiving misspellings (`kyboard` is wrong), or understanding prose answers, **in the checker**.
  That's what Jev's second opinion is for, and it decides only what the checker can't.
- Letting Jev overrule the checker's "right", or the author's "wrong".

## Acceptance criteria

Unit tests in `OneADay.Tests/` pin every rule, and every live teaser has its own accept and reject
cases:

- [x] `AnswerValidationTests.cs` — the rule matrix, including number words and equivalent formula
      forms
- [x] `TeaserBankTests.cs` — every question in the live bank, accepted phrasings and plausible
      wrong answers
- [x] Rules 9 and 10, in `AnswerValidationTests.cs`, each mutation-verified on 2026-10-27
- [x] **The second opinion**, each test mutation-verified on 2026-10-27:
      `AnswerGraderTests.cs` (the order above, the bar, kept verdicts, no verdict asked again,
      formula and number questions, the author's ruling, the cap, the switch and the key),
      `JevClientTests.cs` (the request, and no verdict from an error, an odd reply or a slow one),
      `JudgedAnswersAdminTests.cs` (the review list and both rulings), and
      `SolvedAnswerTests.cs` (an answer Jev passes solves the puzzle)
- [x] **The PR 74 review fixes**, each mutation-verified on 2026-10-28:
      `AnswerGraderTests.cs` (no verdict reused across numbers or where Jev couldn't be asked,
      symbol-only answers, the switch stopping kept verdicts, edits re-asking, the per-address
      share), `AnswerValidationTests.cs` (answer keys, the oversized ratio),
      `JudgedAnswerStoreTests.cs` (making room when full, replacing a verdict after an edit,
      answers saved as typed), `JudgedAnswersAdminTests.cs` (no way to add a visitor's text to
      the accepted answers, Agree with Jev, a ruling while the form is open),
      `SolvedAnswerTests.cs` (one press counts while Jev is asked), and `DeployFilesTests.cs`
      (the verdicts file exists from startup)
- [x] **The request shape**, confirmed with a real key on 2026-10-28: `instructions` alone is
      enough (no `criteria`), the chance comes back at `answers.same_answer.noul`, and replies
      took 130 to 340 ms, well inside the 5 second wait
- [x] **The bar**, set from the 2026-10-28 trial: `AnswerGraderTests.cs` checks the shipped
      bar sits in the gap the trial found, that exactly the bar passes, and that a kept verdict
      follows today's bar; `JudgedAnswersAdminTests.cs` checks admin labels each verdict by it
- [ ] Jev on the live site, and a week of verdicts reviewed before the 70% bar is settled

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

The checker runs in memory; a submission is never logged or inserted into any query. It's
stored only when Jev judges it (`judged-answers.json`), and sent only to Jev, as above. Jev's
client is `Services/JevClient.cs`, and the order is `Services/AnswerGrader.cs`.
`Arithmetic` is hand-written with a **cap on how deeply brackets nest**, so hostile input
(`((((((…`) is rejected instead of crashing the server. A very long *flat* expression never
nests, so callers cap length too: 300 characters on the daily challenge, 120 in the game.

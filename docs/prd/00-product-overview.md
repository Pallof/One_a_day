# This is both a Product Diary and a PRD of past and ongoing developments of Stumpty
# PRD 00 — Product overview

**Status:** Spec of record · **Last updated:** 2026-09-22

## Vision and Problem

Stumpty serves a single brain teaser every day to help people use and stretch
their minds. I think that in this day and age it's too easy to rely on AI and it
can make our minds lazy and reduces our processing abilities. I believe that solving 
a challenge a day can help retain our mental sharpness. Even if you aren't able to
solve it, it's the process of trying and attempting to put the pieces together.
Challenges will vary from simple word riddles, to tougher math questions or simply 
taking a step back and looking at the bigger picture. Regardless of the user, anyone
can benefit from it or enjoy solving a quick challenge.

*The About page's wording, word for word — keep the two in step.*

It is deliberately **small, anonymous and frictionless**: no sign-up, no leaderboard pressure —
just today's puzzle, an answer box, and a hint when you're genuinely stuck.

## The name

The site was **One a Day** until 2026-09-19. It is now **Stumpty** — a play on *stumped*, at
`stumpty.com` — which avoids Bayer's "One A Day" vitamin trademark.

Researched and rejected: **Stumped** (a board game, an iOS trivia app and a live trademark),
**Stumpt** (a gaming YouTube channel of ~500k subscribers making puzzle content — never
worry-free), **Crack It** (crowded with lock-and-code puzzle apps) and **Humpty Stumpty** (used by
several stump-grinding businesses). Only Stumpty had no existing brand, app, game, company or
trademark, with `.com`, `.io` and `.gg` all free.

**The code keeps the old name on purpose.** Visitors never see the internal `OneADay` names or
`oad-` prefixes, and renaming them would churn every file and test for nothing. One internal name
is pinned for a stronger reason: visitors' stored IDs are encrypted under the name `OneADay`, and
letting it follow a renamed project would make every one unreadable ([PRD 11](11-deployment.md)).

## Users

| User | Needs |
|---|---|
| **Solver** (primary) | A quick daily mental workout: instant feedback, a hint if stuck, and how they did against others. Arrives with no account and expects none. |
| **Author** (the site owner) | To publish one teaser a day without friction, queue a week ahead in one sitting, and hear when a question is broken or unfair. |

## Product principles

1. **No accounts.** Nothing about a daily puzzle needs identity, and statistics are just counts —
   answers and correct answers — with nothing about who answered. *History: an early login page
   was removed because it earned nothing.*
   The daily email ([PRD 15](15-email-subscriptions.md)) is the one exception — an address, kept
   only to send that email, never linked to site activity, deleted on unsubscribe. Not an account.
2. **Generous, never wrong.** A right answer is never rejected over formatting — case,
   punctuation, `9` vs `nine`, `48` vs `48 mph` — and a wrong one never passes
   ([PRD 02](02-answer-evaluation.md)).
3. **Effort before help.** Hints and solutions are earned, so the puzzle stays a puzzle
   ([PRD 03](03-hints-and-solutions.md)).
4. **Never spoil the future.** No answer to today's or a scheduled teaser can be reached, even by
   guessing an address.
5. **Data stays legible.** Everything lives in plain text files (JSON) the author can read, edit
   by hand, or back up by copying one folder.

## Scope today

Shipped, and covered by the specs of record:

- The daily challenge — midnight-Pacific rollover, countdown, confetti ([01](01-daily-challenge.md))
- Generous answer checking — numbers, number words, formulas, alternatives ([02](02-answer-evaluation.md))
- Earned hints; solutions published the day after a challenge runs ([03](03-hints-and-solutions.md))
- **No archive** — only today's puzzle can be reached ([04](04-archive-and-discovery.md))
- **Twenty Four** — a game that makes its own puzzles, so it can't use up the teasers ([07](07-twenty-four.md))
- **Recycling** — past teasers come back when none is scheduled, so dry spells don't show and newcomers see the back catalogue ([08](08-recycling-rotation.md))
- Authoring — scheduling, difficulty, private tags, pictures ([05](05-authoring-and-admin.md))
- Suggestions and issue reports, screened for bots ([06](06-community-feedback.md)) and emailed to the author ([14](14-email-notifications.md))
- **Daily email** at 7am Pacific for anyone who confirms their address; one click to leave ([15](15-email-subscriptions.md))
- **Visual design** — editorial, one centred reading column ([09](09-visual-design.md))

**Non-goals** (considered and declined): user accounts, comment threads, per-user profiles and
scores, and any leaderboard.

## Architecture summary

- **C# and Blazor Server, .NET 10.** Pages are built on the server, and each visitor's browser
  keeps a live connection to it.
- **No database.** JSON files in `OneADay/App_Data/` — `teasers.json`, `stats.json`,
  `suggestions.json`, `issues.json`, `rotation.json`, `subscribers.json` — plus the
  `teaser-images/` and `keys/` folders. The folder never goes in the public repository: it holds
  every future answer and every subscriber's address. *History: the folder itself was excluded on
  2026-07-30, before any of it was committed. The authoring draft `BrainTeaserQuestions.txt` —
  questions with hints and solutions — was committed on 2026-07-29 and 2026-08-11 and only excluded
  on 2026-08-26. It stays in the public history, and because merged pull requests keep their old
  changes on GitHub, only making the repository private removes it completely.*
- **One clock.** `AppTime`, pinned to Pacific Time.
- **Admin only on the author's machine.** Teasers are published by copying `teasers.json`
  ([PRD 10](10-admin-authentication.md)).

Storage is the simplest thing that works. All reading and writing goes through the `*Store`
classes, so moving to a database (SQLite) later is a contained change.

## Success measures

- A teaser is live every day (no gaps in the schedule)
- Solvers who submit a correct answer are never rejected on formatting
- Zero incidents of a future answer leaking
- Issue reports trend down as questions improve

## Roadmap

| Phase | Work |
|---|---|
| **Now — launch blockers** | [Deployment](11-deployment.md) |
| **Next — retention** | [Streaks & sharing](12-streaks-and-sharing.md) |
| **Then — author quality of life** | [Content pipeline](13-content-pipeline.md) |

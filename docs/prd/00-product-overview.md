# PRD 00 — Product overview

**Status:** Spec of record · **Last updated:** 2026-09-11

## Vision and Problem

One a Day serves a single brain teaser every day to help people use and stretch
their minds. I think that in this day and age it's too easy to rely on AI and it
can make our minds lazy and reduces our processing abilities. I believe that solving 
a challenge a day can help retain our mental sharpness. Even if you aren't able to
solve it, it's the process of trying and attempting to put the pieces together.
Challenges will vary from simple word riddles, to tougher math questions or simply 
taking a step back and looking at the bigger picture. Regardless of the user, anyone
can benefit from it or enjoy solving a quick challenge.

It is deliberately **small, anonymous, and frictionless**. No sign-up wall, no
prerequisites, no leaderboard pressure — just today's puzzle, an answer box, and
the option of a hint when you're genuinely stuck.

## Users

| User | Needs |
|---|---|
| **Solver** (primary) | A quick daily mental workout. Wants instant feedback, a hint if stuck, and to know how they did relative to others. Arrives with no account and expects none. |
| **Author** (the site owner) | To publish one teaser per day without friction, queue a week ahead in one sitting, and hear when a question is broken or unfair. |

## Product principles

1. **No accounts.** Nothing about a daily puzzle requires identity. Solvers are
   counted by an anonymous per-device ID so aggregate stats work without
   collecting anything personal. *A login page existed early on and was removed
   because it earned nothing.* The daily email ([PRD 15](15-email-subscriptions.md))
   is the one place the site holds anything personal: an address, kept only to send
   that email, never linked to what anyone does on the site, and deleted on
   unsubscribe. It is not an account.
2. **Answer checking should be generous, never wrong.** A solver who knows the
   answer must never be told they're wrong over formatting — case, punctuation,
   `9` vs `nine`, `48` vs `48 mph`. Equally, a wrong answer must never pass.
3. **Effort before help.** Hints and solutions are available but must be earned,
   so the puzzle stays a puzzle.
4. **Never spoil the future.** Today's answer and any scheduled teaser's answer
   must be unreachable, including by URL guessing.
5. **Data stays legible.** All content and metrics live in human-readable JSON the
   author can read, hand-edit, or back up by copying a folder.

## Scope today

Shipped and covered by the specs of record:

- Daily challenge with midnight-Pacific rollover, post-solve countdown, confetti
- Answer evaluation (numeric, word-number, formula, and multi-phrasing matching)
- Earned hints, and solutions that unlock the day after a challenge runs
- **No archive** — only today's puzzle is reachable ([PRD 04](04-archive-and-discovery.md))
- **The Twenty Four game** — endlessly replayable, generates its own puzzles, so it
  can't exhaust the teaser bank ([PRD 07](07-twenty-four.md))
- **Recycling rotation** — once the queue of new teasers runs dry, past ones are drawn
  back out of a box, so the daily habit survives a dry spell and newcomers reach the
  back catalogue ([PRD 08](08-recycling-rotation.md))
- Authoring tools: scheduling, difficulty, backend tags, support images
- Community feedback: teaser suggestions and issue reports with triage, screened for
  automation ([PRD 06](06-community-feedback.md)), and emailed to the author
  ([PRD 14](14-email-notifications.md))
- **Daily email** — the day's challenge at 7am Pacific for anyone who confirms their
  address; one click to leave ([PRD 15](15-email-subscriptions.md))
- **Visual design system** — editorial layout, one centred reading column
  ([PRD 09](09-visual-design.md))

**Explicit non-goals** (considered and declined): user accounts, comment threads,
per-user profiles and scores, and any leaderboard.

## Architecture summary

- **C# / Blazor Server, .NET 10**, interactive server rendering
- **No database.** JSON files in `OneADay/App_Data/`: `teasers.json`,
  `stats.json`, `suggestions.json`, `issues.json`, `rotation.json`,
  `subscribers.json`, plus `teaser-images/`. The folder is gitignored — it holds
  subscriber addresses, so it must never reach the public repository.
- **One clock.** `AppTime` pins every day boundary to `America/Los_Angeles`
- **Admin lives on the author's machine.** The live site has no `/admin`; teasers are
  written locally and published by copying `teasers.json` ([PRD 10](10-admin-authentication.md))

Storage is intentionally the simplest thing that works. All reads and writes funnel
through the `*Store` services, so swapping JSON for SQLite later is a contained
change.

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

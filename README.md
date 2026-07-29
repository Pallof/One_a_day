# One a Day 

**A daily brain teaser to help people use and stretch their minds.**

One new puzzle every day — no accounts, no sign-ups, no prerequisites. Just old
fashioned pen, paper and good ol' effort. Type your answer, get instant feedback,
unlock a hint if you're stuck, and come back tomorrow for the next one.

Built with **C# / Blazor Server (.NET 10)**.

---

## Features

**For solvers**
- **Challenge of the day** — one teaser per day, rolling over at midnight Pacific Time
- **Smart answer checking** — accepts what a human would call correct:
  - case and punctuation insensitive (`A Keyboard!` = `a keyboard`)
  - numeric equivalence (`9` = `9.0`, `1,000` = `1000`)
  - spelled-out numbers (`eighty` = `80`, `forty-eight mph` = `48 mph`)
  - multiple accepted phrasings per question, separated by `;`
- **Hints that must be earned** — locked until you've made at least one attempt
- **Solutions after 3 attempts** — on past questions only; today's answer never leaks
- **Every question gets its own page** — shareable, bookmarkable URLs by date
- **Difficulty badges** — Easy / Medium / Hard, colour-coded green / yellow / red
- **Confetti** on a correct answer 🎉
- **Post-solve countdown** to the next challenge
- **Community stats** — "466/13,502 successful attempts", revealed only after you solve
- **Suggest a teaser** — visitors can submit their own (rate-limited to one per day)

---

## Design decisions worth knowing

- **No accounts.** An early login page was removed — accounts accomplished nothing
  here. Solvers are counted with an anonymous per-device ID, so "unique minds" works
  without collecting anything personal.
- **Pacific-pinned clock.** Every page and the post-solve countdown share one
  `America/Los_Angeles` day boundary, so the timer hitting 00:00:00 and the new
  teaser appearing are always the same moment.
- **Answers are never trusted input.** Submissions are compared in memory and never
  stored or interpolated anywhere; Blazor HTML-encodes everything it renders.

---

## Design origins

The app follows the original wireframes, kept in [`Picture Directory/`](Picture%20Directory):

| Site map | Home page | All questions |
|---|---|---|
| ![site map](Picture%20Directory/One-a-Day-SiteMap.png) | ![home page](Picture%20Directory/Main%20Page.png) | ![all questions](Picture%20Directory/Search_Page--All_questions_page.png) |

There is also a [login page wireframe](Picture%20Directory/login_Page.png) from the
original design, kept for reference — the app deliberately has no accounts today.

> The project began in 2020 as a React sketch; it was rebuilt from these same
> wireframes as a Blazor app.

---

## Status & roadmap

Working today: everything in the feature list above, running locally.

Before deploying publicly:
- [ ] **Protect `/admin`** — it currently has no authentication, which is fine on
      localhost but must be gated (a passphrase or host-level auth) before going live
- [ ] Pick a host and deploy (`dotnet publish` runs on any cheap host)

Ideas after that:
- [ ] Solve streaks and a share button ("One a Day #12 — solved in 2 attempts 🧠")
- [ ] A "queue is running dry" warning when no teaser is scheduled for tomorrow
- [ ] Bulk import so a week of teasers can be added at once

---

## Docs

- [`OneADay/README.md`](OneADay/README.md) — deeper notes on the app, its data
  files, and the answer-validation rules

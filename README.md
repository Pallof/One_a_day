# One a Day 🧠

**A daily brain teaser to help people use and stretch their minds.**

One a Day serves a single brain teaser every day to help people use and stretch
their minds. I think that in this day and age it's too easy to rely on AI and it
can make our minds lazy and reduces our processing abilities. I believe that solving 
a challenge a day can help retain our mental sharpness. Even if you aren't able to
solve it, it's the process of trying and attempting to put the pieces together.
Challenges will vary from simple word riddles, to tougher math questions or simply 
taking a step back and looking at the bigger picture. Regardless of the user, anyone
can benefit from it or enjoy solving a quick challenge.

Built with **C# / Blazor Server (.NET 10)**.

---

## Features

**For solvers**
- **Challenge of the day** — one teaser per day, rolling over at midnight Pacific Time
- **Smart answer checking** — accepts what a human would call correct:
  - case and punctuation insensitive (`A Keyboard!` = `a keyboard`)
  - numeric equivalence (`9` = `9.0`, `1,000` = `1000`)
  - spelled-out numbers (`eighty` = `80`, `forty-eight mph` = `48 mph`)
  - multiple accepted phrasings per question
- **Hints that must be earned** — locked until you've made at least one attempt
- **Solutions unlock the next day** — never while a challenge is live, however many attempts you make
- **Yesterday's solution page** — spoiler warning plus separate blur veils over the question and the answer, so you can still attempt it first
- **Difficulty badges** — Easy / Medium / Hard, colour-coded green / yellow / red
- **Confetti** on a correct answer 🎉
- **Post-solve countdown** to the next challenge
- **Community stats** — "466/13,502 successful attempts", revealed only after you solve
- **Suggest a teaser** — visitors can submit their own (rate-limited to one per day)

**For the author (`/admin`)**
- Add, edit, delete, and schedule teasers; the date defaults to the next free day
  so a whole week can be queued in one sitting
- Set difficulty, attach a support image, and apply backend-only tags for sorting
- Review visitor suggestions and promote good ones straight into the add form
- Per-teaser stats: unique solvers and success rate

---

## Quickstart

Requires the .NET 10 SDK. If it's installed under `~/.dotnet` and isn't on your
PATH yet:

```bash
export PATH="$HOME/.dotnet:$PATH"
```

Run the app:

```bash
dotnet run --project OneADay
```

Then open the URL it prints (e.g. <http://localhost:5178>).

Run the test suite (188 tests covering the answer-matching rules and the full
teaser bank):

```bash
dotnet test OneADay.Tests
```

---

## Adding your daily teaser

Open the **☰ menu → Add a teaser (admin)**, or go straight to `/admin`.

| Field | Notes |
|---|---|
| **Show on date** | The day it becomes the Challenge of the day. Defaults to the next free date. |
| **Difficulty** | Easy / Medium / Hard — shown to solvers as a colour-coded badge. |
| **Question** | The teaser itself. |
| **Answer** | Separate alternative accepted answers with `;` — e.g. `48; 48 mph; forty-eight`. |
| **Tags** | Comma-separated labels (`logic, math`) for your own sorting. Never shown to visitors. |
| **Support image** | Optional PNG/JPG/GIF/WebP up to 3 MB, for puzzles that need a diagram. |
| **Hint** | Unlocks for the solver after their first attempt. |
| **Solution** | Shown once solved, or revealable the day after the challenge runs. |

---

## How it's built

```
OneADay/                     the Blazor app
  Components/
    ChallengeView.razor      shared question + submission + hint + stats UI
    DifficultyBadge.razor    colour-coded Easy/Medium/Hard badge
    Pages/                   Home, Yesterday, TwentyFour, About, Contact, Admin
  Models/BrainTeaser.cs      the teaser + all answer-matching rules
  Services/
    TeaserStore.cs           reads/writes teasers.json
    StatsStore.cs            anonymous attempt statistics
    SuggestionStore.cs       visitor suggestions + rate limiting
    ImageStore.cs            support-image uploads
    AppTime.cs               pins "today" to midnight Pacific
  App_Data/                  ← all data lives here (see below)
OneADay.Tests/               xUnit tests for answer validation
```

| Route | Page |
|---|---|
| `/` | Challenge of the day |
| `/yesterday` | Yesterday's solution — spoiler-veiled question and answer |
| `/twentyfour` | Twenty Four — placeholder, under maintenance |
| `/about` · `/contact` | About, and the suggest-a-teaser form |
| `/admin` | Author tools |

### Where the data lives

There is **no database**. Everything is human-readable JSON in `OneADay/App_Data/`,
so the whole site can be backed up by copying a folder or edited by hand:

> **`App_Data/` is deliberately not in version control.** It holds the question
> bank — including every answer and unpublished teaser — plus visitor-submitted
> text, none of which belongs in a public repo. Back the folder up out-of-band;
> it is the app's only datastore. A fresh clone starts with a few sample teasers
> generated on first run.

### Design decisions worth knowing

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

| Site map | Home page |
|---|---|
| ![site map](Picture%20Directory/One-a-Day-SiteMap.png) | ![home page](Picture%20Directory/Main%20Page.png) |

The original design also included an [all-questions page](Picture%20Directory/Search_Page--All_questions_page.png);
that archive was built and later removed — see [PRD 04](docs/prd/04-archive-and-discovery.md).

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

- [`docs/prd/`](docs/prd) — product requirements: a spec of record for everything
  built, plus proposals for what's next
- [`OneADay/README.md`](OneADay/README.md) — deeper notes on the app, its data
  files, and the answer-validation rules

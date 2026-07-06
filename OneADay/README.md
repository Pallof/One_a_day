# One a Day 🧠

A daily brain teaser web app built with C# Blazor (interactive server rendering),
following the wireframes in `Desktop/One_a_day/Picture Directory/One a Day layout.pdf`.

One new teaser appears each day as the **Challenge of the day**. Visitors type an
answer into the submission box and get instant feedback, with an optional hint after
a couple of wrong attempts.

## Running the app

The .NET 10 SDK is installed at `~/.dotnet`. From this folder:

```sh
export PATH="$HOME/.dotnet:$PATH"   # or add this line to ~/.zshrc once
dotnet run
```

Then open the URL it prints (e.g. http://localhost:5178).

## Adding your daily teaser

Open the **menu (☰) → Add a teaser (admin)**, or go straight to `/admin`:

- **Show on date** — the day the teaser becomes the Challenge of the day.
  The form defaults to the next day that doesn't have one yet, so you can queue
  up a whole week in one sitting.
- **Question** and **Answer** are required. Separate alternative accepted
  answers with `;` (e.g. `9; nine`).

### How answers are validated

- **Text** answers ignore case, spacing, and punctuation (`A Keyboard!` matches
  `a keyboard`).
- **Numbers** compare by value, so `9`, `9.0`, and ` 9 ` all match, and `1,000`
  matches `1000`.
- **Parentheses** create alternatives automatically: storing `12 (a dozen)`
  accepts `12`, `a dozen`, or the full `12 (a dozen)`.
- Alternatives separated by `;` are each checked with all the rules above.

These rules are pinned down by unit tests in `../OneADay.Tests` — run
`dotnet test` from the parent folder after changing the matching logic.
- **Hint** (optional) appears as a concealed click-to-reveal bar between the
  question and the submission box.
- **Solution** (optional) is shown once solved, and on past questions.

The table below the form lists everything scheduled and past, with edit/delete.

## Where the data lives

All teasers are stored in a single human-readable file: `App_Data/teasers.json`.
You can edit it by hand or back it up by copying it. It is created automatically
(with a few sample riddles) on first run.

Attempt statistics live next to it in `App_Data/stats.json` — per teaser: which
anonymous visitors attempted it, total submissions, and how many were correct.
The home page shows a line like "🧠 466 minds have taken on this challenge —
466/13,502 successful attempts", and the admin table has a per-teaser Stats
column. Visitors are counted by a random device id in browser storage (no
accounts involved), so the "unique minds" number is per-device.

## Pages (matching the wireframe site map)

| Route | Wireframe page |
|---|---|
| `/` | Home — welcome banner, Challenge of the day, submission box |
| `/questions` | All Questions — search bar + past questions with reveal-able solutions |
| `/about` | About us |
| `/contact` | Contact us |
| `/admin` | Your input system for adding daily teasers |

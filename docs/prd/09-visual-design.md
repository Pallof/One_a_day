# PRD 09 — Visual design system

**Status:** Spec of record · **Owns:** `wwwroot/app.css`, `Components/Layout/`

## Problem

The original UI was built straight from the wireframes: saturated blue boxes, yellow
banner bars, square corners. It communicated the structure, which is what a wireframe
is for — but it read as a form to fill in rather than something to sit and think with,
and the colour did all the shouting while the puzzle sat quietly in the middle of it.

A puzzle people are asked to concentrate on wants the opposite: a calm page where the
question is the loudest thing on it.

There was also a layout bug that only appeared on real screens, covered below.

## The look

**Editorial, not app-like.** Paper-coloured ground, white cards that sit *on* the page
rather than boxing it in, a serif masthead, and colour used for meaning rather than
decoration.

### Tokens

Defined once at the top of `wwwroot/app.css`. **Colour comes from a token**, with two
allowances: `#fff` for a literal white surface, and `rgba()` for shadows and scrims.

| Role | Token | Value |
|---|---|---|
| Page ground | `--paper` | `#FCFBF6` |
| Card | `--card` | `#FFFFFF` |
| Hairline | `--line` | `#E4E1D6` |
| Text | `--ink` / `--ink-soft` | `#22334D` / `#54627A` |
| Links, information | `--blue` / `--blue-deep` / `--blue-tint` | `#2B7BC7` / `#1E5E9C` / `#EAF3FB` |
| Accent, hints | `--gold` / `--gold-tint` / `--amber` | `#F0A500` / `#FDF3DA` / `#9A6700` |
| Success | `--green` / `--green-deep` / `--green-tint` | `#2F855A` / `#256B49` / `#E7F3EC` |
| Error, hard | `--red` / `--red-tint` | `#B03A2E` / `#F9E9E7` |

Colour carries meaning consistently: **gold** is a hint or a caution, **blue** is
information or a way onward, **green** is success, **red** is wrong or hardest.

### Type

- **Lora** (variable serif, 400–700) — masthead, wordmark, yesterday's answer,
  countdown digits, and the Twenty Four card numerals.
- **Atkinson Hyperlegible** (400/700 + italic) — everything else.
- **System monospace** — expression input and inline examples only.

> **Fraunces was here first, and was replaced 2026-09-10.** Its "wonk" letterforms —
> most visibly an `f` that drops a hooked tail below the baseline — read as a typo at
> masthead size in a heading as ordinary as *"Challenge of the day"*.
>
> Wonk is a real axis in the typeface, so switching it off looked like a one-line fix.
> It isn't: **Google Fonts ships `WONK` as separate static files, not a variable axis.**
> Requesting both instances returns two different woff2 files (19,748 and 19,880 bytes)
> under `@font-face` blocks that are otherwise identical, so the browser picks one
> arbitrarily and `font-variation-settings: "WONK" 0` does nothing — the served file
> has no axes at all. Verified by fetching both and diffing them.
>
> Lora carries the same editorial weight without the quirk, sits closer to Georgia so
> the fallback degrades better, and is **23 KB against Fraunces' 66 KB** — which was
> two-thirds of the font budget for perhaps thirty words a page.

> Atkinson is not just an aesthetic choice. It was commissioned by the Braille
> Institute to make similar glyphs distinguishable — `I`/`l`/`1`, `O`/`0`, `rn`/`m`.
> On a site where people read a riddle carefully and type an exact answer, that is
> functional. Don't swap it for something merely prettier.

Monospace is scoped to Twenty Four on purpose. The daily answer box stays proportional
because its answers are usually prose (*"a keyboard"*); the game's box is always an
expression, where alignment and countable parentheses matter.

## The centring contract

**This is the part most likely to be broken again, so it is a rule rather than a
description.**

> Every full-width band is **full-bleed**. An **inner wrapper** carries the max-width
> *and* `margin-inline: auto`. A page may never set its own max-width.

The original bug: `.oad-static` had `max-width: 720px` and no auto margin. A max-width
**caps** width, it does not centre — so on a wide monitor the whole site hugged the
left edge, while in a narrow window 720px filled the screen and it looked fine. It
survived because it is invisible at the size you develop at.

The same cap-without-centre pattern was in four places (`.oad-static`, `.ys-page`,
`.tf-page`, `.oad-admin`). Width now lives in exactly one place — `MainLayout` — which
is what stops it recurring in a fifth.

- Reading column: **680px**. Header and footer bands: **1040px**, so the rules under
  and above them reach the viewport edges while their contents stay aligned.
- `/admin` opts into the **wide** measure (1040px); the layout applies it, not the page.
- **No page may scroll horizontally.** Wide content — admin tables especially — scrolls
  inside its own `overflow-x` container.

Verified by measurement rather than by eye: equal left/right gutters at 375, 480, 768,
900, 1024, 1280, 1440, 1600, 1920 and 2560px, with no horizontal overflow on any route.

## Navigation

- **Above 900px:** sections inline in the header as pills; the current one is filled
  with `--ink`.
- **900px and below:** the pills are replaced by a hamburger panel.
- Both menus render from **one array** in `MainLayout`, so they cannot drift apart, and
  the active item is computed from the route.
- The panel opens on hover *and* `:focus-within`, which is what makes it work on touch.

## Components

- **Cards** (`.oad-box`) — white, hairline border, soft shadow, 14px radius.
- **Masthead** (`.oad-banner`) — centred Lora with a 64px gold rule beneath.
- **Dateline** — today's date above the masthead ([PRD 01](01-daily-challenge.md)).
- **Difficulty badge** — tinted pill with a coloured dot, not a solid block.
- **Hint bar** — gold while locked or offered, switching to **blue once revealed**, so
  it reads as information rather than a standing offer ([PRD 03](03-hints-and-solutions.md)).
- **Playing cards** — Twenty Four's hand ([PRD 07](07-twenty-four.md)).
- **Reconnect modal** — the Blazor template's circuit-lost dialog. Easy to forget
  because it never shows in normal development, but it appears on every deploy, every
  network blip, and every laptop wake, so for some visitors it is the second thing they
  see. It must use the tokens like anything else.

### The Twenty Four nudge

`Components/TwentyFourNudge.razor` — a panel that slides in from the right edge of the
daily challenge, inviting the visitor to the Twenty Four game.

It exists because the nav link says **"Twenty Four"** and nothing else. A visitor has no
way to know that is a game, let alone one they can play indefinitely — and the daily
challenge is over in a minute, so this is the only route to more.

Three triggers, first one wins; the rest become no-ops:

| Trigger | Threshold | Why there |
|---|---|---|
| Solved | after the solve dialog is **dismissed** | Two panels at once is shouting, and the dialog is the thing they earned |
| Struggling | **3** wrong answers | Enough genuine tries that it reads as an offer rather than "give up" |
| Idle | **5** quiet minutes | Any keystroke resets the clock — someone typing steadily is not stuck |

**It must never become a modal.** No backdrop, no focus steal, no Escape trap. Someone
mid-thought on a hard teaser has to be able to ignore it completely and lose nothing.
It must not overlap the answer box or the report button at any width; below 560px it
comes up from the bottom full-width instead, because a card hanging off the right edge
of a phone would cover the answer box.

**Dismissal is permanent and persisted.** The purpose is discovery — once someone knows
the game exists, showing it again is nagging. A dismissal survives reloads.

> **The thresholds are product judgement, not tuning knobs.** Lowering the attempt count
> or shortening the idle period turns an offer into a pester, and this appears while
> someone is deliberately concentrating. If it ever needs to convert harder, make the
> panel better rather than making it interrupt sooner.

### Removed deliberately

- **Bootstrap.** Nothing in the markup used a Bootstrap class — every class is `oad-*`
  prefixed — so it was ~230KB whose only effect was fighting the reset in `app.css`.
- **Two glossy gradient buttons**, one with a looping animated shine. Nothing else in
  an editorial design has a gradient, and a sweeping highlight beside a puzzle competes
  with the puzzle.

## Non-goals

- A dark theme (the paper ground is the identity)
- A component library or CSS framework
- Animation beyond hover feedback and the existing confetti

## Acceptance criteria

- [x] Equal gutters at every width from 375px to 2560px, on every route
- [x] No route scrolls horizontally; admin tables scroll inside their container
- [x] Both webfonts load and apply (`document.fonts.check`)
- [x] Nav swaps to the hamburger at 900px; the panel opens on touch via `:focus-within`
- [x] No page sets its own max-width
- [x] Bootstrap is gone and nothing regressed

### Not yet converted

The redesign reached the pages a visitor sees. Two surfaces still carry the old
palette, and both are reachable:

- [ ] **`Pages/Admin.razor.css`** — status badges and buttons still use the pre-redesign
      colours (`#8f1a00`, `#d23b34`, `#2e9e3f`, `#1a1a1a`). Author-only, so it is
      cosmetic rather than urgent, but it is the page the author uses most.
- [ ] Remaining hard-coded colours removed in favour of tokens (34 literals at the time
      of the audit; the shared ones — hover tints and the field border — are now
      `--blue-tint-hover`, `--gold-tint-hover` and `--field`)

> **A caught mistake worth recording.** This section originally claimed *"nothing may
> hard-code a hex value"* while 34 literals sat in the stylesheets, and ticked *"no page
> sets its own max-width"* while `Home.razor.css` still held `max-width: 720px` — the
> exact bug this document exists to prevent. It was invisible because the reading column
> is 680px, so a 720px cap never bound; widening the measure would have started clipping
> the home page alone. **A design doc can drift from its own CSS as easily as a spec can
> drift from its code.**

## Implementation notes

`wwwroot/app.css` holds tokens, base type, and the shared `oad-*` primitives.
`Components/Layout/MainLayout.razor{,.css}` owns the bands and the one measure.
Per-component styling stays in scoped `.razor.css` files and must use tokens.

> The redesign restyled the **existing** `oad-*` class names rather than renaming
> anything — `.oad-box` became a white card, `.oad-banner` became a masthead. That is
> why a change this broad landed with no test churn: the bUnit tests assert on those
> class names and all of them kept passing.

Two comments in the CSS are load-bearing and should not be tidied away: the
`display:none` warning on the honeypot decoys ([PRD 06](06-community-feedback.md)),
and the note on `.oad-measure` explaining why a max-width alone is not centring.

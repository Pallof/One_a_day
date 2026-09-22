# PRD 09 — Visual design system

**Status:** Spec of record · **Owns:** `wwwroot/app.css`, `Components/Layout/`

## In plain terms

The site should feel like a page in a well-set book, not an app. Paper-coloured background,
white cards, a serif headline, and colour used to *mean* something rather than to decorate.
Every colour is named once in one file so it can't drift, and the width of the page is set in
exactly one place — because when it was set in five, the whole site quietly hugged the left
edge of wide monitors for weeks.

## Problem

The original UI came straight from the wireframes: saturated blue boxes, yellow banner bars,
square corners. It communicated structure, which is what a wireframe is for, but it read as a
form to fill in rather than something to sit and think with — the colour did all the shouting
while the puzzle sat quietly in the middle.

A puzzle people are asked to concentrate on wants the opposite: a calm page where the question
is the loudest thing on it.

## The look

**Editorial, not app-like.** Paper ground, white cards that sit *on* the page rather than
boxing it in, a serif masthead, colour for meaning.

### Colours

Named once at the top of `wwwroot/app.css` and referenced everywhere else. Two allowances
only: `#fff` for a literal white surface, and `rgba()` for shadows.

| Role | Name | Value |
|---|---|---|
| Page ground | `--paper` | `#FCFBF6` |
| Card | `--card` | `#FFFFFF` |
| Hairline | `--line` | `#E4E1D6` |
| Text | `--ink` / `--ink-soft` | `#22334D` / `#54627A` |
| Links, information | `--blue` / `--blue-deep` / `--blue-tint` | `#2B7BC7` / `#1E5E9C` / `#EAF3FB` |
| Accent, hints | `--gold` / `--gold-tint` / `--amber` | `#F0A500` / `#FDF3DA` / `#9A6700` |
| Success | `--green` / `--green-deep` / `--green-tint` | `#2F855A` / `#256B49` / `#E7F3EC` |
| Error, hard | `--red` / `--red-tint` | `#B03A2E` / `#F9E9E7` |

Meaning is consistent: **gold** is a hint or caution, **blue** is information or a way onward,
**green** is success, **red** is wrong or hardest.

### Type

- **Lora** (serif) — masthead, wordmark, yesterday's answer, countdown digits, Twenty Four
  numerals.
- **Atkinson Hyperlegible** — everything else.
- **System monospace** — expression input and inline examples only.

> **Atkinson is a functional choice, not an aesthetic one.** It was commissioned by the
> Braille Institute to make similar shapes distinguishable — `I`/`l`/`1`, `O`/`0`, `rn`/`m`.
> On a site where people read a riddle carefully and type an exact answer, that matters. Don't
> swap it for something merely prettier.

> **Fraunces was here first, replaced 2026-09-10.** Its quirky letterforms read as a typo at
> masthead size. Switching the quirk off looked like a one-line fix but isn't possible: Google
> Fonts ships it as separate static files, not an adjustable axis, so the setting does nothing.
> Lora carries the same weight without the quirk, falls back to Georgia more gracefully, and is
> **23 KB against 66 KB**.

Monospace is scoped to Twenty Four deliberately. The daily answer box stays proportional
because its answers are usually prose (*"a keyboard"*); the game's box is always an expression,
where alignment and countable parentheses matter.

## The centring contract

**The part most likely to be broken again, so it's a rule rather than a description.**

> Every full-width band spans the whole window. An **inner wrapper** carries both the maximum
> width *and* the automatic side margins that centre it. A page may never set its own maximum
> width.

The original bug: a max-width with no auto margin. A maximum width **caps** how wide something
gets; it does not centre it. So on a wide monitor the whole site hugged the left edge, while in
a narrow window it looked perfect — which is why it survived, because it's invisible at the
size you develop at. The same pattern was in four separate places. Width now lives in exactly
one file, `MainLayout`, which is what stops a fifth.

- Reading column **680px**, shared by the footer. Header contents **1040px** — the nav needs
  the room. Both bands span the full window, so the rules above and below them reach the
  screen edges while their contents stay aligned.
- `/admin` opts into the wide measure; the layout applies it, not the page.
- **No page may scroll sideways.** Wide content — admin tables especially — scrolls inside its
  own container.

Verified by measurement rather than by eye: equal left and right gutters at 375, 480, 768, 900,
1024, 1280, 1440, 1600, 1920 and 2560px, with no sideways overflow on any route.

## Navigation

- **Above 900px:** sections inline in the header as pills, the current one filled.
- **900px and below:** a hamburger panel instead.
- Both menus render from **one list** in `MainLayout`, so they can't drift apart.
- The panel opens on hover *and* on focus, which is what makes it work on touch.

## Components

- **Cards** — white, hairline border, soft shadow, 14px radius.
- **Masthead** — centred Lora with a 64px gold rule beneath.
- **Dateline** — today's date above the masthead ([PRD 01](01-daily-challenge.md)).
- **Difficulty badge** — tinted pill with a coloured dot, not a solid block.
- **Hint bar** — gold while locked or offered, **blue once revealed**, so it reads as
  information rather than a standing offer ([PRD 03](03-hints-and-solutions.md)).
- **Playing cards** — Twenty Four's hand ([PRD 07](07-twenty-four.md)).
- **Reconnect dialog** — the framework's connection-lost message. Easy to forget because it
  never appears in normal development, but it shows on every deploy, network blip and laptop
  wake, so for some visitors it's the second thing they see. It must use the named colours like
  anything else.

### The Twenty Four nudge

A panel that slides in from the right edge of the daily challenge, inviting the visitor to the
game. It exists because the nav link says **"24"** and nothing else — a visitor has no way to
know that's a game, let alone one they can play indefinitely, and the daily challenge is over
in a minute.

Three triggers, first one wins:

| Trigger | Threshold | Why there |
|---|---|---|
| Solved | after the solve dialog is **dismissed** | Two panels at once is shouting, and the dialog is what they earned |
| Struggling | **3** wrong answers | Enough genuine tries that it reads as an offer, not "give up" |
| Idle | **5** quiet minutes | Any keystroke resets it — someone typing steadily isn't stuck |

**It must never become a modal.** No backdrop, no focus steal, no Escape trap. Someone
mid-thought on a hard teaser has to be able to ignore it completely. It must not overlap the
answer box or the report button at any width; below 560px it comes up from the bottom instead,
because a card hanging off the right edge of a phone would cover the answer box.

**Dismissal is permanent and remembered.** The purpose is discovery — once someone knows the
game exists, showing it again is nagging.

> **The thresholds are product judgement, not tuning knobs.** Lowering the attempt count or
> shortening the idle period turns an offer into a pester, and this appears while someone is
> deliberately concentrating. If it ever needs to convert harder, make the panel better rather
> than making it interrupt sooner.

### Removed deliberately

- **Bootstrap** — nothing in the markup used it, so it was ~230KB whose only effect was
  fighting the reset in `app.css`.
- **Two glossy gradient buttons**, one with a looping animated shine. Nothing else here has a
  gradient, and a sweeping highlight beside a puzzle competes with the puzzle.

## Non-goals

- A dark theme — the paper ground is the identity
- A component library or CSS framework
- Animation beyond hover feedback and the existing confetti

## Acceptance criteria

- [x] Equal gutters at every width from 375px to 2560px, on every route
- [x] No route scrolls sideways; admin tables scroll inside their container
- [x] Both webfonts load and apply
- [x] Nav swaps to the hamburger at 900px, and the panel opens on touch
- [x] No page sets its own maximum width
- [x] Bootstrap is gone and nothing regressed

### Not yet converted

The redesign reached the pages a visitor sees. Two surfaces still carry the old palette:

- [ ] **`Pages/Admin.razor.css`** — status badges and buttons still use pre-redesign colours.
      Author-only, so cosmetic rather than urgent, but it's the page the author uses most
- [ ] Remaining hard-coded colours replaced by names (34 at the time of the audit; the shared
      ones are now named)

> **A caught mistake worth recording.** This section once claimed *"nothing may hard-code a
> colour"* while 34 literals sat in the stylesheets, and ticked *"no page sets its own maximum
> width"* while one page still did — the exact bug this document exists to prevent. It was
> invisible because the reading column is 680px and that page's cap was 720px, so it never
> bound. **A design doc can drift from its own CSS as easily as a spec can drift from its code.**

## Implementation notes

`wwwroot/app.css` holds the names, base type and shared primitives.
`Components/Layout/MainLayout.razor{,.css}` owns the bands and the one measure. Per-component
styling stays in scoped `.razor.css` files and must use the names.

> The redesign restyled the **existing** class names rather than renaming anything. That's why
> a change this broad landed with no test churn — the tests assert on those class names and all
> of them kept passing.

Two CSS comments are load-bearing and must not be tidied away: the `display:none` warning on
the decoy fields ([PRD 06](06-community-feedback.md)), and the note explaining why a maximum
width alone is not centring.

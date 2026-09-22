# PRD 09 — Visual design system

**Status:** Spec of record · **Owns:** `wwwroot/app.css`, `Components/Layout/`

## In plain terms

The site should feel like a page in a well-set book, not an app: a paper-coloured background, white
cards, a serif headline, and colour used to *mean* something rather than to decorate. Every colour
is named once, in one file, so it can't drift. The page width is set in exactly one place — because
when it was set in four, the whole site quietly hugged the left edge of wide monitors for weeks.

The first design came straight from the wireframes: saturated blue boxes, yellow banner bars, square
corners. It read as a form to fill in — the colour shouted while the puzzle sat quietly in the
middle. A puzzle people concentrate on wants a calm page where the question is the loudest thing.

## The look

### Colours

Named once at the top of `wwwroot/app.css` and used by name everywhere else. Two exceptions only:
`#fff` for a plain white surface, and `rgba()` for shadows.

| Role | Name | Value |
|---|---|---|
| Page background | `--paper` | `#FCFBF6` |
| Card | `--card` | `#FFFFFF` |
| Hairline | `--line` | `#E4E1D6` |
| Text | `--ink` / `--ink-soft` | `#22334D` / `#54627A` |
| Links, information | `--blue` / `--blue-deep` / `--blue-tint` | `#2B7BC7` / `#1E5E9C` / `#EAF3FB` |
| Accent, hints | `--gold` / `--gold-tint` / `--amber` | `#F0A500` / `#FDF3DA` / `#9A6700` |
| Success | `--green` / `--green-deep` / `--green-tint` | `#2F855A` / `#256B49` / `#E7F3EC` |
| Error, hard | `--red` / `--red-tint` | `#B03A2E` / `#F9E9E7` |

**Gold** means a hint or caution, **blue** information or a way onward, **green** success, **red**
wrong or hardest.

### Type

- **Lora** (serif) — masthead, wordmark, yesterday's answer, countdown digits, Twenty Four numbers.
- **Atkinson Hyperlegible** — everything else.
- **Monospace** — only the Twenty Four answer box and inline examples. Daily answers are usually
  words (*"a keyboard"*), so that box stays in the normal font; the game's box always holds a sum,
  where lined-up characters and countable brackets help.

> **Atkinson is a practical choice, not a pretty one.** The Braille Institute commissioned it to make
> look-alike characters distinct — `I`/`l`/`1`, `O`/`0`, `rn`/`m`. On a site where people read a
> riddle carefully and type an exact answer, that matters. Don't swap it for something prettier.

> **History:** Fraunces was the first headline font, replaced 2026-09-10 — its quirky letter shapes
> read as a typo at headline size. Turning the quirk off looked like a one-line fix but can't be
> done, because Google Fonts ships it as fixed files. Lora has the same weight without the quirk,
> falls back to Georgia more gracefully, and is **23 KB against 66 KB**.

## The centring rule

**The part most likely to be broken again, so it's a rule, not a description.**

> Every full-width band spans the whole window. An **inner wrapper** carries both the maximum width
> *and* the automatic side margins that centre it. A page may never set its own maximum width.

The original bug: a maximum width with no automatic margins. A maximum width **caps** how wide
something gets; it doesn't centre it. On a wide monitor the site hugged the left edge; in a narrow
window it looked perfect — invisible at the size you develop at, which is why it survived. The same
mistake was in four places. Now only the layout (`MainLayout`) applies a width, which stops a fifth.

- Reading column **680px**, shared by the footer. Header contents **1040px**, because the menu needs
  the room. Both bands span the window, so the lines above and below reach the screen edges while
  their contents stay aligned.
- `/admin` uses the wide measure — applied by the layout, not the page.
- **No page may scroll sideways.** Wide content, admin tables especially, scrolls inside its own box.

Checked by measurement, not by eye: equal left and right margins at 375, 480, 768, 900, 1024, 1280,
1440, 1600, 1920 and 2560px, with no sideways scrolling on any page.

### Fitting on one screen

The main pages should fit a **1512 × 860** browser window — a 14-inch laptop — **without a vertical
scrollbar**, so today's puzzle and its answer box are in view at once. The vertical spacing in
`MainLayout` and `app.css` is tuned to that target, and the code comments that cite it point here.
Two decisions came from it: the footer is **one line**, because a second row pushed the home page
past 860px; and the main area's bottom padding was cut from 4.5rem, which on its own pushed every
page about 40px over.

## Navigation

- **Wider than 900px:** sections in the header as pills, the current one filled. **900px and
  below:** a hamburger menu.
- Both menus come from **one list** in `MainLayout`, so they can't drift apart.
- The menu opens on hover *and* on focus, which is what makes it work on touch screens.

## Components

- **Cards** — white, hairline border, soft shadow, 14px rounded corners.
- **Masthead** — centred Lora over a 64px gold rule, with the **dateline** above
  ([PRD 01](01-daily-challenge.md)).
- **Difficulty badge** — a tinted pill with a coloured dot, not a solid block.
- **Hint bar** — gold while locked or on offer, **blue once revealed**, so it reads as information
  rather than a standing offer ([PRD 03](03-hints-and-solutions.md)).
- **Playing cards** — Twenty Four's hand ([PRD 07](07-twenty-four.md)).
- **Reconnect message**, shown when the page's live connection drops. It never appears in normal
  development, so it's easy to forget — but it shows on every deploy, network blip and laptop wake,
  making it the second thing some visitors see. It uses the named colours like everything else.

### The Twenty Four nudge

A panel that slides in from the right of the daily challenge, inviting the visitor to the game. It
exists because the menu link says **"24"** and nothing else — nobody could tell that's a game, let
alone one to play indefinitely — and the daily challenge is over in a minute.

| Trigger (first wins) | When | Why then |
|---|---|---|
| Solved | after the solve dialog is **closed** | Two panels at once is shouting, and the dialog is what they earned |
| Struggling | **3** wrong answers | Enough genuine tries that it reads as an offer, not "give up" |
| Idle | **5** quiet minutes | Any keystroke resets it — someone typing steadily isn't stuck |

**It must never become a pop-up that takes over the page** — no dimmed backdrop, no stealing the
cursor, no trapping the Escape key — so someone mid-thought on a hard teaser can ignore it
completely. It must never cover the answer box or report button; below 560px it rises from the
bottom instead, because a card hanging off the right edge of a phone would cover the answer box.

**Closing it is permanent and remembered.** Its job is discovery; once someone knows the game
exists, showing it again is nagging.

> **The thresholds are product judgement, not tuning knobs.** A lower attempt count or shorter idle
> time turns an offer into pestering someone who is concentrating. To make it convert better, make
> the panel better — don't make it interrupt sooner.

### Removed on purpose

- **Bootstrap** (a ready-made style library) — nothing used it, so it was ~230 KB whose only effect
  was fighting this site's own styles. The page no longer loads it, but its unused files (8.4 MB)
  are still in `wwwroot/lib/bootstrap/` and still go out with every publish.
- **Two glossy gradient buttons**, one with a looping animated shine. Nothing else has a gradient,
  and a moving highlight beside a puzzle competes with the puzzle.

## Non-goals

- A dark theme — the paper background is the identity
- A component library or style framework
- Animation beyond hover feedback and the confetti

## Acceptance criteria

- [x] Equal gutters at every width from 375px to 2560px, on every route
- [x] No route scrolls sideways; admin tables scroll inside their container
- [x] Both webfonts load and apply
- [x] Nav swaps to the hamburger at 900px, and the panel opens on touch
- [x] No page sets its own maximum width
- [x] Bootstrap is no longer loaded, and nothing regressed

### Not yet converted

- [ ] **The last hard-coded colours — all 14 now in `Components/Pages/Admin.razor.css`**, mostly
      status badges and buttons — replaced by names. There were 34 across the site at the audit;
      everywhere else is down to the allowed `#fff`. Admin is author-only, so this is cosmetic
      rather than urgent, but it's the page the author uses most

> **History — a caught mistake:** this section once claimed *"nothing may hard-code a colour"* while
> 34 sat in the stylesheets, and ticked *"no page sets its own maximum width"* while one page still
> did — the very bug this doc exists to prevent. Nobody noticed because that page's cap (720px) was
> wider than the 680px column, so it never took effect. **A design doc can drift from its own styles
> as easily as a spec can drift from its code.**

## Implementation notes

`wwwroot/app.css` holds the colour names, base type and shared pieces;
`Components/Layout/MainLayout.razor` and its `.css` own the bands and the one measure. Each
component's own `.razor.css` must use the names.

> The redesign restyled the **existing** class names rather than renaming any, which is why a change
> this broad needed no test changes: the tests check those names, and all kept passing.

Two style comments do real work and must not be tidied away: the `display:none` warning on the decoy
fields ([PRD 06](06-community-feedback.md)), and the note on why a maximum width alone doesn't
centre anything.

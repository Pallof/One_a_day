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
- Today's challenge and yesterday's solution use a **split** measure (`oad-measure-split`): the
  reading column up to 1100px wide, the 1040px wide measure from there on, for their two columns
  (below). The layout applies it too.
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

**On a laptop, today's challenge is two columns** *(author's decision, 2026-10-23)*. A picture on
every question pushed the home page about 350px past a laptop's window, and even with no picture
it was over a 15-inch MacBook Pro's 790px. From 1100px wide the question and its picture sit on the
left; the hint, the answer box and the way to yesterday's solution on the right. Narrower than
that, so on phones, tablets and laptop windows that aren't full width, it's the single column as
before.

**A long question's picture gives way.** In the two columns the picture takes what the question
leaves: the window's height, less the rest of the page (377px, measured), less the question's
lines. The server counts those as the question's length divided by 50 characters, which never
undercounts a question in the bank, because the left column is the same width on every laptop. The
picture is never taller than its usual size or shorter than 140px. Measured over all 25 questions:

| Window | Fits without scrolling | Pictures made smaller |
|---|---|---|
| 1512 × 860 (14-inch laptop, the target) | 25 of 25 | 2 |
| 1440 × 790 (15-inch MacBook Pro) | 25 of 25 | 5 |
| 1920 × 1000 | 25 of 25 | none |
| 1536 × 730 (15.6-inch Windows laptop at 125%) | 23 of 25 | the two longest still scroll, by 20 and 50px |

**Yesterday's solution follows suit** *(author's decision, 2026-10-23)*. At 1440 × 790 it was
1,055px tall. From 1100px wide the question and its picture sit on the left; the spoiler
warning, the solution it warns about and the way back on the right. The warning moved beside the
solution rather than across the top: the question itself gives nothing away. The markup keeps the
phone's order, warning first, and the grid places each part, so phones read it as before. A long
question's picture gives way by the same rule (the rest of this page is 401px, measured with the hint bar that has since gone, so it now has room to spare).
Measured over all 25 questions, with each one's own solution:

| Window | Fits without scrolling |
|---|---|
| 1512 × 860 | 24 of 25 |
| 1440 × 790 | 24 of 25 |
| 1920 × 1000 | 24 of 25 |
| 1536 × 730 | 22 of 25 |

The one that never fits is held up by its solution, not its picture: the blurred solution takes
its full height even before it's revealed, and that one alone is taller than the window.

## Navigation

- **Wider than 900px:** sections in the header as pills, the current one filled. **900px and
  below:** a hamburger menu.
- Both menus come from **one list** in `MainLayout`, so they can't drift apart.
- The menu opens on hover *and* on focus, which is what makes it work on touch screens.
- **Below the sections, the phone menu ends with *Report an issue*** on every page — an action, not
  a place, so a button in the soft ink colour rather than a link ([PRD 06](06-community-feedback.md)).
  The wide menu has no room for it, so there the floating button stands in, on every page but
  About. The two share the 900px breakpoint.

## Components

- **The mascot** — Stumpty, a puzzled yellow duck sitting on a tree stump *(the author's own
  drawing, finished with an image generator; adopted 2026-10-14)*. See [below](#the-mascot).
- **Cards** — white, hairline border, soft shadow, 14px rounded corners.
- **Masthead** — centred Lora over a 64px gold rule, with the **dateline** above
  ([PRD 01](01-daily-challenge.md)).
- **Difficulty badge** — a tinted pill with a coloured dot, not a solid block.
- **Hint bar** — gold while locked or on offer, **blue once revealed**, so it reads as information
  rather than a standing offer ([PRD 03](03-hints-and-solutions.md)).
- **Playing cards** — Twenty Four's hand ([PRD 07](07-twenty-four.md)).
- **Floating buttons** — round or pill-shaped, white, hairline border, soft shadow, 1.25rem from
  the bottom corners: *Report an issue* bottom right ([PRD 06](06-community-feedback.md)), the
  **scratch pad** bottom left on the challenge page ([PRD 01](01-daily-challenge.md)). The
  Twenty Four nudge comes in from the right, above the report button. Nothing else floats.
- **Reconnect message**, shown when the page's live connection drops. It never appears in normal
  development, so it's easy to forget — but it shows on every deploy, network blip and laptop wake,
  making it the second thing some visitors see. It uses the named colours like everything else.

### The mascot

**The whole duck on its stump wherever the page shows it; its face alone as the browser icon**
*(author's decision, 2026-10-14)*. The stump is half the name, so the page never crops it off.

| Where | What | Size |
|---|---|---|
| Header, beside the name | Whole duck | 40px tall — as tall as it goes without making the header taller (the section pills are 40.8px) |
| Footer tagline | Whole duck | the height of the words |
| About, beside the opening paragraph | Whole duck, with a description for screen readers | 140px wide; 96px on a phone |
| The solve dialog, at the top | Whole duck — it replaced 🎉, as the confetti already celebrates | 96px tall |
| The not-found page | Whole duck — it replaced a 🐱 left from before the mascot (2026-10-15) | 96px tall |
| The emails' header, beside the name | Whole duck, loaded from the site | 34px tall. Added 2026-10-18: the brain emoji had stayed there after the site dropped it |
| Browser tab | The face, close up | 32px |
| A phone's home screen | The face | 180px |

It replaced the brain emoji, which looked different on every device, so the site's mark wasn't the
same picture for everyone. It also replaced Blazor's own logo, which the tab had shown since the
project was created. The tab icon goes through `Assets`, so its address changes with the file;
browsers hold on to an old tab icon otherwise.

- **Two sizes are served** from `wwwroot/img/`: 120px tall for the header and footer (18 KB) and
  400px for About and the dialog (121 KB). That way the header on every page doesn't load the
  big one.
- **The originals live in `docs/brand/`, never `wwwroot/`.** Everything in `wwwroot/` is served
  to anyone who asks, and a large file there is what a bot would fetch (see Bootstrap, under
  *Removed on purpose*).
- **Image generators fake transparency.** Asked for a see-through version, they paint a grey and
  white checkerboard into the picture. Before using a new drawing, check the file really has an
  alpha channel (`sips -g hasAlpha`). The current cut-out was made with macOS's own background
  removal (Finder → Quick Actions → Remove Background).
- **Its colours aren't from the palette** (yellow, orange and wood brown), and that's fine for a
  picture. The duck's yellow sits beside `--gold`, which is why it looks at home. The palette rule
  is for the styles.

### The link preview

What iMessage, WhatsApp, Slack, Discord, Facebook, LinkedIn and X show when someone shares a link
to the site *(added 2026-10-15)*. A bare link from a stranger looks suspicious; a picture, a name
and a line saying what the site is lets someone decide before they tap.

- **Title:** *Can you solve today's puzzle?* An invitation rather than a brand name: the apps
  show "Stumpty" as the site name beside it, and the card carries it too.
- **Description:** *Free, no sign-up. A new brain teaser every day: riddles, logic and math
  puzzles.* The reassurance comes first because WhatsApp cuts the description off after two
  lines. Search engines get the same words.
- **Picture:** the duck beside the name and the tagline, on the paper colour, 1200 × 630, the
  shape all of these apps expect (`wwwroot/img/stumpty-card.png`). The tagline is large and
  bold so it can still be read where Slack and Discord show the card small. iMessage shows
  only the picture and the title, so there the card is the only thing that says what the
  site is.

*History: the first version (2026-10-15) had the title "Stumpty — one puzzle a day", led the
description with the puzzles and ended with "Free, with no account needed", and set the tagline
in light grey at 42pt. Shown cold to a reviewer who knew nothing of the site, it read as "an
ad for a brand, not an invitation to play": WhatsApp cut off the reassurance, the tagline was
unreadable in Slack and Discord, and "Stumpty" appeared up to four times. All three were changed
the same day. Its other ideas were turned down: a sample puzzle on the card (a spoiler, and
the bank handed out one teaser at a time), and "takes 2 minutes" (not true of the hard ones).*

Rules:

- **It's in the page as first sent.** The apps' servers read the page without running scripts,
  so the tags are in App's `<head>` (`Components/LinkPreview.razor`), not added by a page.
- **The same for every page, and never today's puzzle.** A preview shows up in a chat before
  anyone has chosen to look. Putting the question in it would spoil the puzzle and hand the bank
  out one teaser a day.
- **The picture's address is absolute** (`https://stumpty.com/img/…`), because the apps fetch it
  from their own servers. It's built from `Site__BaseUrl`, the same setting the emails use, so a
  wrong value there breaks the preview silently too (PRD 11).
- **Keep the card under 300 KB.** WhatsApp is known to skip larger pictures. It's 182 KB as a PNG,
  which keeps the lettering sharp.
- **To change the words on the card,** edit and rerun `docs/brand/link-card.swift`; its comment
  gives the command. The card goes through `Assets`, so a new card gets a new address.
- **The apps remember previews.** A link already shared keeps its old preview in that
  conversation, and Facebook and LinkedIn keep theirs for days. To refresh them after a change,
  paste the link into Facebook's Sharing Debugger or LinkedIn's Post Inspector.

**Checking it on the live site:** after a deploy, paste `https://stumpty.com` into a preview
checker such as opengraph.xyz, or send it to yourself. If the preview comes up blank, look first
at Cloudflare's bot settings: the apps' fetchers are bots, and Cloudflare must let them through.

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
  was fighting this site's own styles. The page stopped loading it first, but its 8.4 MB of files
  stayed in `wwwroot/lib/bootstrap/` until 2026-09-24. They were deleted because the server still
  handed them to anyone who asked. The largest, at 664 KB, was the most expensive thing a bot could
  make the server send.
- **Two glossy gradient buttons**, one with a looping animated shine. Nothing else has a gradient,
  and a moving highlight beside a puzzle competes with the puzzle.
- **Em-dashes and emoji, from everything the site shows or sends** *(author's decision,
  2026-10-27)*: pages, page titles, emails, admin and the app's own log messages. Both made the
  site read as machine-written. Sentences use a full stop, comma or colon instead, and titles and
  subjects use a middle dot: "Stumpty · About". The big emoji above the sign-up, confirm and
  unsubscribe messages went with nothing in their place, and the floating report button has a
  drawn mark instead of the warning sign. Two kinds of symbol stay because they do a job: the
  close buttons' cross (✕) and the card suits on Twenty Four. `CopyStyleTests` holds the rule:
  it reads every page, component and script, leaving out the comments.

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
- [x] **Today's challenge on a laptop**, checked by measurement on 2026-10-23: two columns from
      1100px with no sideways scroll; one column, at 680px, at 1024px and on a 375px phone; every
      question in the bank fits 1512 × 860 and 1440 × 790, the long ones with a smaller picture
- [x] **What the two columns rely on** — `ChallengeLayoutTests`, each mutation-verified: the
      question's line count for its picture, 50 characters a line; the answer box and
      yesterday's link in one column; only today's challenge widening the page
- [x] Bootstrap is no longer loaded or shipped, and nothing regressed. Its address returns 404, and
      every route loads all of its files
- [x] **The mascot** — `MascotTests`, each mutation-verified: the whole duck beside the name (with
      no alt text), in the footer tagline, on About (described for screen readers) and in the
      solve dialog, each one a file `wwwroot` serves; the tab and home-screen icons linked through
      `Assets`, at 32px and 180px
- [x] The mascot, checked by hand: with the 40px duck the header stays 65.8px tall on a wide
      screen and 69px on a phone. At 375px, About's text wraps round a 96px duck with no sideways
      scroll. The solve dialog with its duck fits 1512 × 860 without scrolling
- [x] **The link preview** — `LinkPreviewTests`, each mutation-verified: the title, description
      and site name; search and preview descriptions match; the picture's address is absolute,
      the card is 1200 × 630 as the tags say and under 300 KB; the tag for X's large picture; the
      tags are in App's `<head>`
- [x] The link preview, checked against the running site as a link bot sees it: every page sends
      all the tags with no script run, and the picture's address returns the PNG
- [ ] The link preview on the live site, in a preview checker and in a real chat — after the
      next deploy

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

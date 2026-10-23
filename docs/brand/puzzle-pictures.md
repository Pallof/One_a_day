# Puzzle pictures

Every question gets a picture, one that helps people picture the puzzle or just makes them smile
([PRD 05](../prd/05-authoring-and-admin.md)). All of them are made the same way, below, and the author
reviews each one before it goes on its question *(author's decision, 2026-10-23; before that a
picture was a nice-to-have)*.

## The rules for every picture

- **Never hint at the answer.** It sets the scene; it doesn't solve anything. Draw a misleading
  layout if the honest one would give it away (the poles puzzle draws its poles far apart).
- **1200×675, under 200 KB.** The same shape as the link-preview card, so it suits the page and the
  email. PNG for diagrams, JPEG or WebP for painted pictures.
- **Describe it in words** when adding it in admin. A diagram's description carries every number
  on it.
- **No logo.** Ducks are welcome; Stumpty on its stump stays the site's mark, not a puzzle prop.
- **Measurements help.** Where a puzzle turns on lengths, angles or distances, the picture may show
  the measurements the question gives, and the unknown as `?`, the way the poles diagram does. Only
  numbers the question states, never the answer, and nothing that draws out the method.
- **Not the wrong answer either.** A trick question's picture mustn't act out the tempting wrong
  answer: a race with the hero out in front says "first place".
- **It must read on a phone**, where it's about 275px wide: a few large figures, not a crowd.
- **Counts are right.** Puzzle people count. Where the question gives a number of things, draw
  exactly that many, or too many to count.

## Making one

[`tools/pictures/pictures.py`](../../tools/pictures/pictures.py) does the mechanical steps, and
Claude follows the `puzzle-pictures` skill (`.claude/skills/puzzle-pictures/`), which runs them in
order. A note at the start of each Claude session lists the questions still without a picture.

1. **`todo`** lists the questions without a picture and how far each has got.
2. **The prompt** goes in `OneADay/App_Data/picture-prompts.md`, private like the bank it describes.
   Each entry's heading carries the question's date, which ties the picture to its question.
3. **Gemini** draws it: the cartoon prompt below, with `docs/brand/stumpty.png` attached.
4. **`take`** files the download, **`prepare`** crops it to 1200×675, matches its cream to the
   page and shrinks it under 200 KB, and the entry gets an `**Alt:**` description.
5. **`sheet`** makes review sheets for the author. Nothing is attached without their yes.
6. **`attach`** puts the approved pictures and descriptions on their questions, backing up the
   bank first. The live site gets them when the bank goes up ([PRD 11](../prd/11-deployment.md)).

## Diagrams

Drawn by hand as SVG in the site's colours (below) and Atkinson Hyperlegible, then rendered to PNG
with headless Chrome:

```bash
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless=new \
  --hide-scrollbars --window-size=1200,675 --screenshot=diagram.png file:///path/to/diagram.html
```

Labels go on the drawing itself, numbers exactly as the question gives them, and the unknown as a
variable (`d = ?`).

**Labels at least 60px tall on a 1200px drawing**, about 5% of its width. On a phone the picture is
only about 275px wide, inside the question card, which brings 60px labels down to about 14px. The
first poles diagram used 26px labels and came out at 6px on a phone, unreadable.

## Cartoons

Made with an image generator (Gemini), with `docs/brand/stumpty.png` attached as the style
reference. Paste the house style below, then describe the scene after **THE SCENE:**. Check what
comes back: nothing that hints at the answer, no garbled lettering, and a real background rather
than a painted checkerboard (check with `sips -g hasAlpha`). Then prepare it with `pictures.py prepare`.

```
Create an illustration for a daily brain-teaser website called Stumpty.

STYLE: friendly cartoon, like a modern children's picture book. Clean dark-navy outlines,
flat colours with soft, simple shading. Use the attached duck as the reference for how
ducks look: round yellow bodies, orange beaks and feet, big expressive eyes. Draw NEW duck
characters in that style. Do not include the tree stump, and do not copy the attached
duck itself.

COLOURS: warm cream background (#FCFBF6), navy (#22334D), golden yellow (#F0A500),
sky blue (#2B7BC7), leaf green (#2F855A). Bright and cheerful, nothing dark or scary.

FORMAT: landscape, 16:9, at least 1600 pixels wide. A solid cream background that fills the
whole image: no transparency, no checkerboard pattern, no border or frame.

COMPOSITION: one clear scene with the main action in the middle and empty space around the
edges. It must still read when shown small on a phone.

NO TEXT: no words, letters or numbers anywhere: no signs, labels, captions, speech
bubbles, or writing on paper. No logo or watermark.

MOST IMPORTANT: the picture sets the scene and makes people smile. It must never hint at
the answer to the puzzle.

THE SCENE:
```

For a picture that shows measurements, swap the NO TEXT paragraph for this one, and put each label
in the scene in quotes, exactly as it should appear:

```
LABELS: this picture shows the puzzle's measurements, like a textbook diagram laid over
the cartoon. Draw ONLY the labels given in THE SCENE, spelled exactly as written, in
large bold dark-navy lettering on a plain part of the background, big enough to read on
a phone. Measuring lines are thin navy lines with small end ticks. No other words,
letters or numbers anywhere: no signs, captions or speech bubbles. No logo or watermark.
```

Check those labels letter by letter: an image generator often misspells or misplaces them. If it
can't get one right after a few tries (a clock's hands are a common failure), draw it as a diagram
instead.

## Colours

| Name | Hex |
|---|---|
| Paper (background) | `#FCFBF6` |
| Ink | `#22334D` |
| Soft ink | `#54627A` |
| Gold | `#F0A500` |
| Blue | `#2B7BC7` |
| Green | `#2F855A` |
| Wood brown | `#9A6700` |

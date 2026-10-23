---
name: puzzle-pictures
description: Make the picture for a Stumpty question, the same way every time - Gemini through Claude in Chrome, checked, cropped, reviewed by the author, then attached. Use whenever a question has no picture (the session-start note lists them), when questions are added to the bank, or when the author asks for a picture to be made, redone or attached.
---

# Puzzle pictures

Every question gets a picture (the author's decision, 2026-10-23), all made the same way. The house
style, the rules and the prompt blocks are in `docs/brand/puzzle-pictures.md`; read it first. The
mechanical steps are `python3 tools/pictures/pictures.py` (run it with no arguments for its help).

The author reviews every picture before it's attached. Nothing goes on a question until they say so.

Everything that describes the bank stays in `OneADay/App_Data/`, which git leaves out: the prompts
(`picture-prompts.md`), the pictures, and the review sheets. Never put a question, its prompt or its
picture in the repo, a commit, a PR or an Artifact.

## 1. See what needs a picture

```bash
python3 tools/pictures/pictures.py todo
```

It lists every question without a picture attached and how far each has got. A new question has no
prompt yet; it takes the next number the command suggests.

## 2. Write the prompt

Add an entry to `OneADay/App_Data/picture-prompts.md`, in date order with the others:

```
## 26 · 2026-08-14 · Short title (Easy)

**Check:** what must not appear (the answer, the hint, the classic wrong answer, any numbers).

(the house style block from docs/brand/puzzle-pictures.md, ending in THE SCENE: and the scene)
```

The date must be the question's date in `teasers.json`: it is what ties the picture to the question.
Read the question, its answer, hint and solution, then design a scene that:

- never hints at the answer, and never shows the **classic wrong answer** either (a race picture with
  the hero in front shows "first place");
- has a few large figures, not a crowd: on a phone the picture is about 275px wide;
- gets every count right where the question gives one (exactly nine people, two oases);
- has no lettering, unless the puzzle turns on measurements: then use the LABELS block with only the
  numbers the question states and `?` for the unknown.

The Check line stays out of what you paste into Gemini: naming a thing to avoid makes it get drawn.

## 3. Generate it in Gemini

Use Claude in Chrome (load its skill and tools first), in a new tab, signed in as the author.

1. Open `https://gemini.google.com/app` (a new chat for each picture).
2. Click "Upload & tools" (the + in the prompt box), then `find` the file inputs and use
   `file_upload` on the image one (the second, `accept="image/*"`) with `docs/brand/stumpty.png`.
   Never click the file input itself.
3. Put the prompt block in with `javascript_tool`:
   `document.querySelector('rich-textarea .ql-editor')` → `focus()` → `document.execCommand('insertText', false, prompt)`.
   A synthetic paste leaves the box empty. Send with the Return key.
4. Wait about 30 seconds, then `zoom` into the result and check it against the Check line, the rules
   above, and for any lettering, a painted checkerboard, a border, or the tree stump.
5. A small fix (remove lettering, shrink something) is an edit in the same chat. A change of layout
   or count needs a new chat with a reworded scene: edits keep the composition. Update the entry's
   scene to what worked.

To fix a picture the author made outside these prompts, upload their picture instead of the
duck and ask for only the change: "Keep everything else exactly as it is."

## 4. Download and file it

Downloading pictures that pass the checks is approved (the author, 2026-10-22). One at a time:

1. Start `python3 tools/pictures/pictures.py take NN-short-name` in the background.
2. Hover the picture and click "Download full size" (top right). It takes up to a minute; stay on
   that chat until `take` reports the file. Leaving stalls the download.
3. Look at the filed picture. Gemini's file names repeat and a stalled download can land late, so
   only your eyes confirm it's the right picture.

Close the tab when done.

## 5. Prepare it

```bash
python3 tools/pictures/pictures.py prepare NN-short-name           # centre 16:9
python3 tools/pictures/pictures.py prepare NN-short-name x y w h   # or a crop box, in the original's pixels
```

It crops, shifts the cream background to the site's `#FCFBF6`, and saves 1200×675 under 200 KB in
`picture-final/`. Look at the result. Crop when there's an inner border, hard edges, or the scene sits
off to one side.

## 6. Describe it

Under the entry's prompt, add one line: `**Alt:** what the picture shows`. It is what screen readers
read out and what email shows when pictures are held back. Describe the scene plainly; for a picture
with labels, give every label and number on it. Never the answer.

## 7. The author's review

```bash
python3 tools/pictures/pictures.py sheet              # everything waiting for review
python3 tools/pictures/pictures.py sheet 26 27        # or just these
```

It makes PNG sheets (question, picture, description, what was checked) in `App_Data/picture-review/`.
Send them with `SendUserFile` (`display: "render"`). Send PNGs, never an HTML page with the pictures
embedded: that didn't render for the author. For a redo, show the before and after side by side.

Wait for their verdict. Redo what they reject, from step 3.

## 8. Attach the approved ones

```bash
python3 tools/pictures/pictures.py attach 26 27
```

Stop the app first (`preview_stop`): it keeps the bank in memory and would write over the change, and
`attach` refuses while it runs. It backs up `teasers.json`, copies each picture into `teaser-images/`
under a random name as the admin page would, and sets its description. `--replace` swaps a picture a
question already has.

The live site gets the pictures only when `teasers.json` and `teaser-images/` are uploaded
(PRD 11). That is a Fly command: ask the author first, every time.

#!/usr/bin/env python3
"""The mechanical steps of giving every question a picture.

How the pictures are made and why: docs/brand/puzzle-pictures.md. Claude follows the
puzzle-pictures skill (.claude/skills/puzzle-pictures/SKILL.md), which runs these steps.
Everything here works on OneADay/App_Data, which git leaves out because it holds the bank.

  pictures.py todo                   questions with no picture attached, and how far each has got
  pictures.py hook                   the same, as session-start context for Claude; silent when done
  pictures.py take NN-name           file the next Gemini download as picture-originals/NN-name.jpeg
  pictures.py prepare NN-name [x y w h]
                                     crop (default: centre 16:9), match the background to the site's
                                     cream, 1200x675 under 200 KB, into picture-final/NN-name.jpg
  pictures.py sheet [NN ...]         review sheets (PNG) for the author, in App_Data/picture-review/;
                                     no numbers means every picture waiting for review
  pictures.py attach [--replace] NN ...
                                     attach approved pictures and their descriptions to teasers.json

NN is the entry's number in App_Data/picture-prompts.md, whose headings read
"## NN · 2026-08-14 · title (Easy)". The date ties the entry to its question.
"""
import html
import json
import os
import re
import shutil
import subprocess
import sys
import time
import uuid
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
# STUMPTY_APP_DATA points the tool at a copy, for trying it out without touching the real bank.
APP = Path(os.environ.get("STUMPTY_APP_DATA", ROOT / "OneADay" / "App_Data"))
TEASERS = APP / "teasers.json"
PROMPTS = APP / "picture-prompts.md"
ORIGINALS = APP / "picture-originals"
FINAL = APP / "picture-final"
IMAGES = APP / "teaser-images"
REVIEW = APP / "picture-review"
BUILD = ROOT / "tools" / "pictures" / ".build"
DOWNLOADS = Path.home() / "Downloads"
CHROME = "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
PICTURE_TYPES = (".jpg", ".jpeg", ".png")

HEADING = re.compile(r"^## (\d+) · (\d{4}-\d{2}-\d{2}) · (.+)$", re.M)


def fail(message):
    sys.exit(message)


def load_teasers():
    with open(TEASERS, encoding="utf-8-sig") as f:
        return json.load(f)


def entries():
    """The prompts file's entries by number: date, title, check line, description, opt-out."""
    text = PROMPTS.read_text(encoding="utf-8") if PROMPTS.exists() else ""
    marks = list(HEADING.finditer(text))
    found = {}
    for i, m in enumerate(marks):
        body = text[m.end(): marks[i + 1].start() if i + 1 < len(marks) else len(text)]
        alt = re.search(r"^\*\*Alt:\*\*\s*(.+)$", body, re.M)
        check = re.search(r"^\*\*Check:\*\*\s*(.+)$", body, re.M)
        nn = int(m.group(1))
        if nn in found:
            fail(f"picture-prompts.md has two entries numbered {nn}.")
        found[nn] = {
            "nn": nn, "date": m.group(2), "title": m.group(3),
            "alt": alt.group(1).strip() if alt else "",
            # Without the italic history notes, e.g. "*(Reworked 2026-10-22 after design review.)*"
            "check": re.sub(r"\s*\*\(.*?\)\*", "", check.group(1)).strip() if check else "",
            "skip": "**No picture.**" in body,
        }
    dates = [e["date"] for e in found.values()]
    for d in set(dates):
        if dates.count(d) > 1:
            fail(f"picture-prompts.md has two entries for {d}.")
    return found


def picture(folder, nn):
    """The entry's picture in a folder: NN-name.jpeg and the like."""
    if not folder.exists():
        return None
    found = sorted(p for p in folder.glob(f"{nn:02d}-*") if p.suffix.lower() in PICTURE_TYPES)
    return found[0] if found else None


def status():
    """Every question without a picture attached, oldest first, with how far it has got."""
    teasers = sorted(load_teasers(), key=lambda t: t["Date"])
    by_date = {e["date"]: e for e in entries().values()}
    rows = []
    for t in teasers:
        name = t.get("ImageFileName")
        if name and (IMAGES / Path(name).name).exists():
            continue
        e = by_date.get(t["Date"])
        if e and e["skip"]:
            continue
        if name:
            state = "attached, but its file is missing from teaser-images"
        elif e is None:
            state = "new: no prompt yet"
        elif picture(FINAL, e["nn"]):
            state = "waiting for review" if e["alt"] else "prepared, needs its **Alt:** description"
        elif picture(ORIGINALS, e["nn"]):
            state = "made, not prepared yet"
        else:
            state = "prompt written, picture not made yet"
        rows.append((e, t, state))
    return rows


def todo():
    rows = status()
    known = {t["Date"] for t in load_teasers()}
    for e in entries().values():
        if e["date"] not in known:
            print(f"Entry {e['nn']} is dated {e['date']}, which matches no question. Fix its heading.")
    if not rows:
        print("Every question has its picture.")
        return
    next_nn = max(entries(), default=0) + 1
    print(f"{'NN':>3}  {'date':<10}  {'level':<6}  {'state':<40}  question")
    for e, t, state in rows:
        nn = f"{e['nn']:>3}" if e else "  —"
        question = " ".join(t["Question"].split())
        print(f"{nn}  {t['Date']:<10}  {t['Difficulty']:<6}  {state:<40}  {question[:60]}")
    if any(e is None for e, _, _ in rows):
        print(f"\nNew questions take the next numbers, from {next_nn}.")


def hook():
    """Session-start context: tells Claude which questions still need a picture. Says nothing in a
    checkout without the bank (a worktree, a fresh clone) or when every question has one."""
    if not TEASERS.exists():
        return
    try:
        rows = status()
    except SystemExit as problem:
        rows, note = [], f" (pictures.py could not read the prompts file: {problem})"
    else:
        note = ""
    if not rows and not note:
        return
    groups = {}
    for _, t, state in rows:
        groups.setdefault(state, []).append(t["Date"])
    summary = "; ".join(f"{state}: {', '.join(dates)}" for state, dates in groups.items())
    message = (
        "Stumpty puzzle pictures: every question gets a picture (the author's decision, 2026-10-23), "
        f"and {len(rows)} question(s) have none attached yet{note}. {summary}. "
        "Offer to make them with the puzzle-pictures skill; the author reviews every picture before "
        "it is attached. If the author's request is about something else, do that first and mention "
        "this in one line at the end of your reply. Details: python3 tools/pictures/pictures.py todo")
    print(json.dumps({"hookSpecificOutput": {"hookEventName": "SessionStart", "additionalContext": message}}))


def take(name):
    if not re.fullmatch(r"\d+-[a-z0-9-]+", name):
        fail("Name it NN-short-name, e.g. 26-paper-boats.")
    ORIGINALS.mkdir(parents=True, exist_ok=True)
    seen_file = ORIGINALS / ".taken"

    def newest(prefix):
        found = [p for p in DOWNLOADS.iterdir() if p.name.startswith(prefix)]
        return sorted(found, key=lambda p: p.stat().st_mtime, reverse=True)

    if not seen_file.exists():
        # The first run counts everything already in Downloads as old.
        old = newest("Gemini_Generated_Image_") + newest(".com.google.Chrome.")
        seen_file.write_text("".join(p.name + "\n" for p in old))
    seen = set(seen_file.read_text().split("\n"))

    start = time.time()
    source = None
    while source is None and time.time() - start < 150:
        fresh = [p for p in newest("Gemini_Generated_Image_") if p.name not in seen]
        if fresh:
            source = fresh[0]
            break
        # Chrome sometimes never finishes saving and leaves a complete JPEG under a temporary name.
        if time.time() - start > 18:
            for p in newest(".com.google.Chrome."):
                if p.name in seen:
                    continue
                size = p.stat().st_size
                time.sleep(5)
                data = p.read_bytes()
                if size and len(data) == size and data[:2] == b"\xff\xd8" and data[-2:] == b"\xff\xd9":
                    source = p
                break
        if source is None:
            time.sleep(3)
    if source is None:
        fail("No new Gemini download after 2½ minutes. Chrome may be waiting on a prompt in its own window.")

    ext = source.suffix.lower() if source.name.startswith("Gemini_") else ".jpeg"
    for old in ORIGINALS.glob(f"{name}.*"):
        if old.suffix.lower() in PICTURE_TYPES:
            v = 1
            while (ORIGINALS / f"v{v}" / old.name).exists():
                v += 1
            (ORIGINALS / f"v{v}").mkdir(exist_ok=True)
            old.rename(ORIGINALS / f"v{v}" / old.name)
            print(f"Kept the earlier {old.name} in picture-originals/v{v}/.")
    shutil.copy2(source, ORIGINALS / f"{name}{ext}")
    with open(seen_file, "a") as f:
        f.write(source.name + "\n")
    print(f"{source.name} -> picture-originals/{name}{ext}")
    print("Look at it before going on: Gemini's file names repeat, and a late download can land instead.")


def prepare_tool():
    """Compiles prepare.swift the first time, trying each installed SDK when the default one
    doesn't match the compiler."""
    source = ROOT / "tools" / "pictures" / "prepare.swift"
    exe = BUILD / "prepare"
    if exe.exists() and exe.stat().st_mtime >= source.stat().st_mtime:
        return exe
    BUILD.mkdir(parents=True, exist_ok=True)
    sdks = sorted(Path("/Library/Developer/CommandLineTools/SDKs").glob("MacOSX*.sdk"),
                  key=lambda p: [int(n) for n in re.findall(r"\d+", p.name)], reverse=True)
    result = None
    for extra in [[]] + [["-sdk", str(s)] for s in sdks]:
        result = subprocess.run(["swiftc", "-O", *extra, str(source), "-o", str(exe)],
                                capture_output=True, text=True)
        if result.returncode == 0:
            return exe
    fail("Couldn't compile prepare.swift with any installed SDK:\n" + result.stderr[-1500:])


def prepare(name, box):
    if len(box) not in (0, 4):
        fail("A crop box is four numbers: x y width height, in the original's pixels.")
    source = next((p for p in sorted(ORIGINALS.glob(f"{name}.*")) if p.suffix.lower() in PICTURE_TYPES), None)
    if source is None:
        fail(f"No picture-originals/{name}.jpeg (or .png).")
    FINAL.mkdir(parents=True, exist_ok=True)
    subprocess.run([str(prepare_tool()), str(source), str(FINAL / f"{name}.jpg"), *box], check=True)
    print(f"Look at picture-final/{name}.jpg before it goes to the author.")


def render(page, png):
    """Screenshots an HTML page at its full height with headless Chrome."""
    probe = subprocess.run([CHROME, "--headless=new", "--allow-file-access-from-files", "--window-size=1240,900",
                            "--virtual-time-budget=4000", "--dump-dom", page.as_uri()],
                           capture_output=True, text=True)
    height = re.search(r'data-h="(\d+)"', probe.stdout)
    subprocess.run([CHROME, "--headless=new", "--hide-scrollbars", "--allow-file-access-from-files",
                    f"--window-size=1240,{height.group(1) if height else 2400}", "--virtual-time-budget=4000",
                    f"--screenshot={png}", page.as_uri()], capture_output=True, check=True)


SHEET_STYLE = """
body{margin:0;background:#FCFBF6;color:#22334D;font:17px/1.45 "Atkinson Hyperlegible",system-ui,sans-serif;padding:24px 30px}
h1{margin:0 0 14px;font-size:1.4rem}
.card{background:#fff;border:1px solid #E6E2D6;border-radius:12px;padding:16px;margin-bottom:16px}
.card h2{margin:0 0 6px;font-size:1.1rem} .q{margin:0 0 12px;max-width:80ch}
img{display:block;width:100%;border-radius:8px;border:1px solid #E6E2D6}
dl{margin:10px 0 0;display:grid;grid-template-columns:auto 1fr;gap:4px 12px;font-size:.95rem;color:#54627A}
dt{font-weight:700;color:#22334D} dd{margin:0} .missing{color:#B42318;font-weight:700}
"""


def sheet(numbers):
    es = entries()
    teasers = {t["Date"]: t for t in load_teasers()}
    if not numbers:
        numbers = [e["nn"] for e, _, state in status() if e and state == "waiting for review"]
        if not numbers:
            fail("Nothing is waiting for review. Name the pictures to show: pictures.py sheet 26 27")
    items = []
    for nn in numbers:
        e = es.get(nn) or fail(f"No entry {nn} in picture-prompts.md.")
        t = teasers.get(e["date"]) or fail(f"Entry {nn} is dated {e['date']}, which matches no question.")
        pic = picture(FINAL, nn) or fail(f"Entry {nn} has no prepared picture yet (pictures.py prepare).")
        items.append((e, t, pic))
    REVIEW.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    pages = [items[i:i + 3] for i in range(0, len(items), 3)]
    made = []
    for k, page in enumerate(pages, 1):
        cards = []
        for e, t, pic in page:
            alt = html.escape(e["alt"]) if e["alt"] else '<span class="missing">Not written yet</span>'
            cards.append(
                f'<section class="card"><h2>{e["nn"]} · {t["Date"]} · {html.escape(t["Difficulty"])}</h2>'
                f'<p class="q">{html.escape(" ".join(t["Question"].split()))}</p>'
                f'<img src="{pic.as_uri()}" alt="">'
                f'<dl><dt>Description</dt><dd>{alt}</dd>'
                f'<dt>Checked for</dt><dd>{html.escape(e["check"]) or "—"}</dd></dl></section>')
        title = f"Pictures for review, {k} of {len(pages)}" if len(pages) > 1 else "Pictures for review"
        doc = ('<!doctype html><html><head><meta charset="utf-8">'
               '<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Atkinson+Hyperlegible:wght@400;700&display=swap">'
               f"<style>{SHEET_STYLE}</style></head><body><h1>{title}</h1>{''.join(cards)}"
               "<script>onload=()=>document.body.dataset.h=document.documentElement.scrollHeight</script>"
               "</body></html>")
        page_file = REVIEW / f"review-{stamp}-{k}.html"
        page_file.write_text(doc, encoding="utf-8")
        png = REVIEW / f"review-{stamp}-{k}.png"
        render(page_file, png)
        made.append(png)
    print("\n".join(str(p) for p in made))


def app_running():
    """The dev app keeps the bank in memory and writes all of it back on its next save, which
    would undo an attach made while it runs."""
    found = subprocess.run(["pgrep", "-f", r"bin/Debug/net[0-9.]+/OneADay"], capture_output=True)
    if found.returncode == 0:
        return True
    for port in (5178, 5025):
        if subprocess.run(["lsof", "-nP", f"-iTCP:{port}", "-sTCP:LISTEN", "-t"], capture_output=True).returncode == 0:
            return True
    return False


def attach(numbers, replace):
    if not numbers:
        fail("Name the approved pictures: pictures.py attach 26 27")
    if app_running():
        fail("The app is running. It keeps the bank in memory and would write over this change on its next "
             "save. Stop it, attach, then start it again.")
    es = entries()
    data = load_teasers()
    by_date = {t["Date"]: t for t in data}
    plan = []
    for nn in numbers:
        e = es.get(nn) or fail(f"No entry {nn} in picture-prompts.md.")
        t = by_date.get(e["date"]) or fail(f"Entry {nn} is dated {e['date']}, which matches no question.")
        pic = picture(FINAL, nn) or fail(f"Entry {nn} has no prepared picture (pictures.py prepare).")
        if not e["alt"]:
            fail(f"Entry {nn} has no **Alt:** description. Every picture is described in words (PRD 05).")
        if t.get("ImageFileName") and not replace:
            fail(f"The {e['date']} question already has a picture. Pass --replace to swap it.")
        if pic.stat().st_size > 3 * 1024 * 1024:
            fail(f"{pic.name} is over the app's 3 MB limit.")
        plan.append((e, t, pic))

    backup = APP / f"teasers.json.bak-{datetime.now():%Y%m%d-%H%M%S}"
    shutil.copy2(TEASERS, backup)
    IMAGES.mkdir(parents=True, exist_ok=True)
    for e, t, pic in plan:
        old = t.get("ImageFileName")
        # A random name, as the app gives an upload (PRD 05): the picture's own name never goes live.
        name = uuid.uuid4().hex + (".png" if pic.suffix.lower() == ".png" else ".jpg")
        shutil.copy2(pic, IMAGES / name)
        t["ImageFileName"] = name
        t["ImageAlt"] = e["alt"]
        if old and not any(o.get("ImageFileName") == old for o in data):
            (IMAGES / Path(old).name).unlink(missing_ok=True)
        print(f"{e['nn']} -> {t['Date']}: {pic.name} attached as {name}")
    temp = TEASERS.with_suffix(".json.tmp")
    temp.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    temp.replace(TEASERS)
    print(f"Backup of the bank before this: {backup.name}")
    print("The live site gets these only when teasers.json and teaser-images/ go up (PRD 11). Ask first.")


def main(args):
    if not args or args[0] in ("-h", "--help", "help"):
        print(__doc__)
        return
    command, rest = args[0], args[1:]
    if command == "todo":
        todo()
    elif command == "hook":
        hook()
    elif command == "take" and len(rest) == 1:
        take(rest[0])
    elif command == "prepare" and rest:
        prepare(rest[0], rest[1:])
    elif command == "sheet":
        sheet([int(n) for n in rest])
    elif command == "attach":
        replace = "--replace" in rest
        attach([int(n) for n in rest if n != "--replace"], replace)
    else:
        fail(__doc__)


if __name__ == "__main__":
    main(sys.argv[1:])

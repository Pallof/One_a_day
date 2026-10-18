"""Checks a restored copy of Stumpty's App_Data without printing anything private.

Runs on a throwaway Fly machine with the restored volume mounted (PRD 11, requirement 8):
    python3 -I check-restore.py /restore

For each file: size, whether it parses, a shape summary (counts, dates), and the first 12
hex digits of its SHA-256, so files unchanged since the snapshot can be matched against the
live server. subscribers.json gets a count only: no hash, no fields. A parse failure names
only the exception type, since its message can quote the file.
Exits 1 if any expected file is missing or anything fails to parse.
"""
import hashlib
import json
import os
import sys
import xml.etree.ElementTree as ET

EXPECTED = ["teasers.json", "rotation.json", "stats.json", "subscribers.json", "metrics.json"]
NO_HASH = {"subscribers.json"}


def shape(name, doc):
    if name in ("teasers.json", "subscribers.json"):
        return f"list of {len(doc)}"
    if name == "stats.json":
        return f"{len(doc)} teasers with stats"
    if name == "rotation.json":
        history = doc["History"]
        last = max(run["Date"] for run in history) if history else "none"
        return f"History {len(history)} days (last {last}), Box {len(doc['Box'])}, ShowCounts {len(doc['ShowCounts'])}"
    if name == "metrics.json":
        days = doc["Days"]
        return f"Days {len(days)} (last {max(days) if days else 'none'}), SavedAtUtc {doc.get('SavedAtUtc')}"
    if name == "suggestions.json":
        return f"Suggestions {len(doc['Suggestions'])}, Log {len(doc['Log'])}"
    if name == "issues.json":
        return f"Issues {len(doc['Issues'])}"
    if name == "weekly-summary.json":
        return f"LastWeekSent {doc['LastWeekSent']}"
    return type(doc).__name__


def main(root):
    failed = False
    seen = set()
    for dirpath, dirs, files in os.walk(root):
        dirs[:] = sorted(d for d in dirs if d != "lost+found")
        for name in sorted(files):
            path = os.path.join(dirpath, name)
            rel = os.path.relpath(path, root)
            seen.add(rel)
            with open(path, "rb") as f:
                data = f.read()
            line = f"{rel}: {len(data)} bytes"
            try:
                if name.endswith(".json"):
                    line += ", parses, " + shape(name, json.loads(data))
                elif name.endswith(".xml"):
                    key = ET.fromstring(data)
                    line += f", parses, <{key.tag}> expires {key.findtext('expirationDate')}"
            except (ValueError, KeyError, TypeError, ET.ParseError) as e:
                line += f", FAILED ({type(e).__name__})"
                failed = True
            if name not in NO_HASH:
                line += ", sha256 " + hashlib.sha256(data).hexdigest()[:12]
            print(line)
    for name in EXPECTED:
        if name not in seen:
            print(f"{name}: MISSING")
            failed = True
    if not any(r.startswith("keys" + os.sep) and r.endswith(".xml") for r in seen):
        print("keys/*.xml: MISSING")
        failed = True
    print("RESULT:", "FAIL" if failed else "OK", f"({len(seen)} files)")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1]))

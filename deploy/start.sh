#!/bin/sh
# Starts Stumpty on Fly once its question bank is on the volume.
#
# The site refuses to start without App_Data/teasers.json, on purpose (PRD 10), and the bank
# is never built into the image: it goes straight from the author's Mac to the volume. A new
# volume starts empty, so instead of crashing and restarting in a loop, wait here for it.
set -eu

data=/app/App_Data
bank="$data/teasers.json"

if [ ! -d "$data" ]; then
    echo "No volume at $data. Check the [[mounts]] section in fly.toml (PRD 11)." >&2
    exit 1
fi

if [ ! -f "$bank" ]; then
    echo "No question bank yet. From the repo on your Mac, run:"
    echo "  fly ssh sftp put OneADay/App_Data/teasers.json $bank"
    while [ ! -f "$bank" ]; do
        sleep 5
    done
    sleep 5   # let the upload finish before the site reads it
    echo "Question bank arrived; starting."
fi

exec dotnet OneADay.dll

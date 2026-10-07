#!/bin/sh
# Copies the live site's counting files down to this machine, for admin's "Site metrics"
# section (PRD 16). Run from the repo folder:
#
#     sh deploy/pull-live-data.sh
#
# An allowlist, never a folder: a private file added to App_Data later stays behind unless
# someone adds it here on purpose. Never add subscribers.json — this machine holds the real
# Gmail password, so a real subscriber list here could mail real people — or anything in
# keys/. DeployFilesTests fails if either appears.
#
# Nothing is ever copied back up. The live site is the only writer of these files, and
# sending them up would wipe out whatever it counted in the meantime (PRD 10).
#
# Stop the site on this machine first and start it again afterwards. It reads stats.json
# and rotation.json once, at startup, and its next save would write those old copies back
# over the download. So the script refuses while it's running (DeployFilesTests checks).
set -eu

FILES="metrics.json stats.json rotation.json"

if [ ! -f fly.toml ]; then
    echo "Run this from the repo folder, where fly.toml is." >&2
    exit 1
fi

if pgrep -x OneADay > /dev/null 2>&1; then
    echo "Stop the site running on this machine first, then start it again after the download." >&2
    echo "It holds stats.json and rotation.json in memory and would save its old copies over the new ones." >&2
    exit 1
fi

for name in $FILES; do
    case "$name" in
        subscribers.json | keys | keys/* | */*)
            echo "Refusing to copy $name: it must never leave the live site." >&2
            exit 1
            ;;
    esac
done

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

for name in $FILES; do
    fly ssh sftp get "/app/App_Data/$name" "$tmp/$name"
done

# Swap them in only once every file has arrived, so a failed download never leaves this
# machine with a mix of live and local files.
for name in $FILES; do
    mv "$tmp/$name" "OneADay/App_Data/$name"
done

echo "Copied $FILES from the live site. Start the site, open /admin and expand Site metrics."

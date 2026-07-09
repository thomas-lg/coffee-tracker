#!/bin/sh
# Unraid/LinuxServer-style PUID/PGID handling. The container starts as root so it
# can re-own the bind-mounted volumes, then drops to the requested user to run the
# app. Defaults match Unraid's `nobody:users` (99:100).
set -e

PUID="${PUID:-99}"
PGID="${PGID:-100}"

# Running as root defeats the whole non-root posture — fail loudly rather than
# silently handing the app uid 0 via gosu. Validate digits-only first (fail closed
# on garbage), then compare numerically so non-canonical zeros like "00" are
# refused too.
for id in "$PUID" "$PGID"; do
    case "$id" in
        ''|*[!0-9]*)
            echo "docker-entrypoint: PUID/PGID must be numeric (got PUID='${PUID}' PGID='${PGID}')." >&2
            exit 1
            ;;
    esac
done
if [ "$PUID" -eq 0 ] || [ "$PGID" -eq 0 ]; then
    echo "docker-entrypoint: refusing PUID/PGID of 0 — set them to a non-root host user." >&2
    exit 1
fi

# /config and /photos are bind-mounted from the host (e.g. Unraid appdata) and
# arrive owned by whoever owns the host directory — usually NOT the app user. That
# mismatch is why SQLite reports "unable to open database file". Re-own them to the
# requested PUID/PGID so the app (run as that user below) can read/write — but only
# when the dir's owner doesn't already match, so a large /photos tree isn't
# recursively re-walked on every start.
for dir in /config /photos; do
    mkdir -p "$dir"
    if [ "$(stat -c '%u:%g' "$dir")" != "${PUID}:${PGID}" ]; then
        chown -R "${PUID}:${PGID}" "$dir"
    fi
done

# Drop privileges (gosu accepts numeric uid:gid directly — no user account needed)
# and hand off to the API. exec replaces PID 1 so signals reach dotnet.
exec gosu "${PUID}:${PGID}" dotnet CoffeeTracker.Api.dll "$@"

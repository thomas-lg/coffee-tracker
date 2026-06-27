#!/bin/sh
# Unraid/LinuxServer-style PUID/PGID handling. The container starts as root so it
# can re-own the bind-mounted volumes, then drops to the requested user to run the
# app. Defaults match Unraid's `nobody:users` (99:100).
set -e

PUID="${PUID:-99}"
PGID="${PGID:-100}"

# /config and /photos are bind-mounted from the host (e.g. Unraid appdata) and
# arrive owned by whoever owns the host directory — usually NOT the app user. That
# mismatch is why SQLite reports "unable to open database file". Re-own them to the
# requested PUID/PGID so the app (run as that user below) can read/write.
mkdir -p /config /photos
chown -R "${PUID}:${PGID}" /config /photos

# Drop privileges (gosu accepts numeric uid:gid directly — no user account needed)
# and hand off to the API. exec replaces PID 1 so signals reach dotnet.
exec gosu "${PUID}:${PGID}" dotnet CoffeeTracker.Api.dll "$@"

# syntax=docker/dockerfile:1
# Production image: build the Angular app, publish the API, and serve the SPA
# same-origin from the API on :8080. Built for linux/amd64 and linux/arm64.

# --- Stage 1: build the Angular PWA ---
# Pinned to the BUILD platform, not the target: this stage emits static files, which have
# no architecture. Letting it follow the target would run the whole Angular build under
# QEMU for an arm64 image, for bytes that would come out identical.
# node:24-slim -- keep in step with ci.yml's node-version and the devcontainer's node
# feature. 24 is the current LTS. Majors are pinned deliberately, not by parity: the gate
# is frontend/package.json's engines (^20.19 || ^22.12 || ^24, i.e. Angular's supported
# range) plus LTS status -- an even major is still "Current" until the October of its
# release year. Dependabot ignores node majors here; move all three references at once.
FROM --platform=$BUILDPLATFORM node:24-slim@sha256:2fe369e969550cde8e867afc3fe370b260140cab4a23d467074295b42163d553 AS web
WORKDIR /web
# Restore deps in their own layer (cached until a manifest changes). This is an npm
# workspaces repo, so `npm ci` needs every member's package.json present up front —
# the lockfile links @coffee-tracker/* to packages/*; without their manifests the
# install fails. `packages/app` is the Angular app, not a workspace member, so it has
# no package.json and is copied with the sources below.
COPY frontend/package.json frontend/package-lock.json ./
COPY frontend/packages/admin/package.json ./packages/admin/
COPY frontend/packages/auth/package.json ./packages/auth/
COPY frontend/packages/coffees/package.json ./packages/coffees/
COPY frontend/packages/data/package.json ./packages/data/
COPY frontend/packages/ui/package.json ./packages/ui/
COPY frontend/packages/util/package.json ./packages/util/
# @playwright/test is a devDependency for e2e only; the image never runs it, so
# skip its multi-hundred-MB browser download during install.
ENV PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1
RUN npm ci
COPY frontend/ ./
RUN npx ng build app --configuration production
# → /web/dist/app/browser

# --- Stage 2: publish the API ---
# mcr.microsoft.com/dotnet/sdk:10.0
#
# Also pinned to the BUILD platform. The publish is portable — no -r/-a, so the output is
# architecture-neutral IL plus a runtimes/ folder carrying every native asset, and the
# entrypoint starts it through the `dotnet` muxer rather than the apphost. The runtime
# stage below is what makes the image arm64 or amd64.
#
# Cross-compiling instead (-a $TARGETARCH) is not an option here: a RID-specific restore
# fails --locked-mode with NU1004, because the committed lock files carry no runtime
# identifiers. Emulating this stage would cost minutes per build for no difference in
# output.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d AS api
WORKDIR /src
# Restore in its own layer (cached until a manifest or lock file changes), the same shape
# as the npm restore in stage 1 — otherwise every edit to any .cs file re-resolves and
# re-downloads the whole package graph. Only the Api's transitive closure is restored, so
# the test project's dependencies never enter the image's build.
COPY backend/Directory.Build.props ./backend/
COPY backend/CoffeeTracker.Api/CoffeeTracker.Api.csproj backend/CoffeeTracker.Api/packages.lock.json ./backend/CoffeeTracker.Api/
COPY backend/CoffeeTracker.Application/CoffeeTracker.Application.csproj backend/CoffeeTracker.Application/packages.lock.json ./backend/CoffeeTracker.Application/
COPY backend/CoffeeTracker.Domain/CoffeeTracker.Domain.csproj backend/CoffeeTracker.Domain/packages.lock.json ./backend/CoffeeTracker.Domain/
COPY backend/CoffeeTracker.Infrastructure/CoffeeTracker.Infrastructure.csproj backend/CoffeeTracker.Infrastructure/packages.lock.json ./backend/CoffeeTracker.Infrastructure/
# --locked-mode for the same reason CI uses it: the image must be built from the exact
# package graph the committed lock files describe, not whatever resolves today.
RUN dotnet restore backend/CoffeeTracker.Api/CoffeeTracker.Api.csproj --locked-mode
COPY backend/ ./backend/
RUN dotnet publish backend/CoffeeTracker.Api/CoffeeTracker.Api.csproj -c Release -o /publish --no-restore

# --- Stage 3: runtime ---
# mcr.microsoft.com/dotnet/aspnet:10.0
# The only stage that follows the target platform, and the only one that needs to: the
# apt packages below (tesseract, gosu, curl) are the image's native dependencies.
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c AS runtime
# OCR via the tesseract CLI (the app shells out to it). The tesseract-ocr package
# pulls its own runtime libs; tesseract-ocr-eng ships eng.traineddata. gosu drops
# privileges in the entrypoint. curl is only for the HEALTHCHECK (the aspnet image
# ships neither curl nor wget). TESSDATA_PREFIX is the parent of the tessdata dir.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        tesseract-ocr tesseract-ocr-eng \
        gosu curl \
    && rm -rf /var/lib/apt/lists/*

# PUID/PGID default to Unraid's nobody:users. Override at runtime to match whoever
# owns the host appdata dirs, so the bind-mounted volumes are writable. HOME points
# into the /config volume so the non-root user's ASP.NET Data Protection key ring
# persists there instead of warning and falling back to ephemeral keys.
ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/5 \
    ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/config/coffee.db" \
    Storage__PhotosPath=/photos \
    FileLog__Directory=/config/logs \
    PUID=99 \
    PGID=100 \
    HOME=/config

WORKDIR /app
COPY --from=api /publish ./
COPY --from=web /web/dist/app/browser ./wwwroot
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

# The entrypoint starts as root to re-own the volumes for PUID/PGID, then uses gosu
# to run the app as that (non-root) user. Don't set USER here — the entrypoint drops
# privileges itself after fixing ownership.
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]

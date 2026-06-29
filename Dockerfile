# syntax=docker/dockerfile:1
# Production image: build the Angular app, publish the API, and serve the SPA
# same-origin from the API on :8080. Built for linux/amd64 (the Unraid NAS).

# --- Stage 1: build the Angular PWA ---
FROM node:26-slim AS web
WORKDIR /web
# Restore deps in their own layer (cached until a manifest changes). This is an npm
# workspaces repo, so `npm ci` needs every member's package.json present up front —
# the lockfile links @coffee-tracker/* to packages/*; without their manifests the
# install fails. `packages/app` is the Angular app, not a workspace member, so it has
# no package.json and is copied with the sources below.
COPY frontend/package.json frontend/package-lock.json ./
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
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY backend/ ./backend/
RUN dotnet publish backend/CoffeeTracker.Api/CoffeeTracker.Api.csproj -c Release -o /publish

# --- Stage 3: runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# OCR via the tesseract CLI (the app shells out to it). The tesseract-ocr package
# pulls its own runtime libs; tesseract-ocr-eng ships eng.traineddata. gosu drops
# privileges in the entrypoint. TESSDATA_PREFIX is the parent of the tessdata dir.
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        tesseract-ocr tesseract-ocr-eng \
        gosu \
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
ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]

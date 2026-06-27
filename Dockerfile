# syntax=docker/dockerfile:1
# Production image: build the Angular app, publish the API, and serve the SPA
# same-origin from the API on :8080. Built for linux/amd64 (the Unraid NAS).

# --- Stage 1: build the Angular PWA ---
FROM node:22-slim AS web
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
# Native OCR libs — the SAME set as dev. TESSDATA_PREFIX is the PARENT of tessdata
# (Tesseract appends /tessdata at runtime).
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        tesseract-ocr tesseract-ocr-eng libtesseract-dev libleptonica-dev \
    && rm -rf /var/lib/apt/lists/*

ENV TESSDATA_PREFIX=/usr/share/tesseract-ocr/5 \
    ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Default="Data Source=/config/coffee.db" \
    Storage__PhotosPath=/photos

WORKDIR /app
COPY --from=api /publish ./
COPY --from=web /web/dist/app/browser ./wwwroot

# Persisted, writable data dirs owned by the image's non-root `app` user. Everything
# else stays read-only.
RUN mkdir -p /config /photos && chown -R app:app /config /photos
USER app

EXPOSE 8080
ENTRYPOINT ["dotnet", "CoffeeTracker.Api.dll"]

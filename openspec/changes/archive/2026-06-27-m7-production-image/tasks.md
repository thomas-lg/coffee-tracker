## 1. Production Dockerfile

- [x] 1.1 Stage 1 `node:22` → `ng build app --configuration production`
- [x] 1.2 Stage 2 .NET 10 SDK → `dotnet publish` the API
- [x] 1.3 Stage 3 ASP.NET 10 (Ubuntu Noble) → apt Tesseract set + `TESSDATA_PREFIX`; copy publish + Angular `browser` → `wwwroot`; `ASPNETCORE_URLS=http://+:8080`; non-root `app`; `/config` + `/photos` writable; env defaults for connection string + photos path

## 2. Program.cs prod wiring

- [x] 2.1 `UseHsts()` in prod (no `HttpsRedirection`; TLS at the proxy)
- [x] 2.2 `UseStaticFiles()` for `wwwroot` + `MapFallbackToFile("index.html")` (prod only)
- [x] 2.3 (already present) `UseForwardedHeaders`, migrate-on-startup, SQLite WAL

## 3. Compose + dockerignore

- [x] 3.1 `docker-compose.yml` (local/reference): one service, `/config` + `/photos` volumes, env (`Jwt__Key`, `REGISTRATION_ENABLED`)
- [x] 3.2 `.dockerignore` already excludes node_modules/bin/obj/.git/*.db/photos

## 4. CI/CD

- [x] 4.1 Drop the `Dockerfile` guard in `ci.yml` `docker-build` (builds amd64, no push)
- [x] 4.2 Drop the `Dockerfile` guard in `release.yml` `image` (build + push)

## 5. Verify

- [x] 5.1 `dotnet build`/`dotnet test` green with the prod wiring (80 tests)
- [ ] 5.2 CI `docker-build` builds the image for linux/amd64 (no local Docker in the dev container)
- [ ] 5.3 On a Docker host: `JWT_KEY=$(openssl rand -base64 48) docker compose up --build` → app on :8080 serves the SPA; `/scan` works (real OCR); data + photos persist across `down/up`

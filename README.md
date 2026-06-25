# Coffee Tracker

A self-hosted web app to catalog the coffees I buy and rate them to taste, with
multiple users each keeping their own ratings. Runs on a NAS via Docker.

## Stack
- **Backend:** ASP.NET Core Web API (.NET 10), EF Core + SQLite, ASP.NET Core
  Identity + JWT.
- **Frontend:** Angular 22 (standalone components, signals, Signal Forms),
  shipped as an installable PWA.
- **Snap-to-fill:** photograph a coffee bag → open-source OCR (Tesseract first,
  behind a swappable `IOcrService`) pre-fills the Add Coffee form.
- **Deploy:** Docker Compose, single image, volumes for the SQLite DB + photos.

See [PLAN.md](./PLAN.md) for the full design and build milestones.

## Status
Planning. Implementation not started yet.

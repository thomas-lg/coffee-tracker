## Why

Adding a coffee means typing in everything off the bag. Milestone M5 in `PLAN.md` adds **snap-to-fill**: photograph the bag, OCR the label, and return best-effort pre-filled fields the user can correct before saving. This is the backend half (the M6 PWA wires the camera + form); it exercises swappable-implementation DI, image-stream handling, and options binding.

## What Changes

- Add a driven port `IOcrService` (`Task<OcrResult> ReadAsync(Stream image, …)`) with **two adapters selected by `Ocr:Engine`**:
  - `tesseract` — the `TesseractOCR` NuGet over the system Tesseract libs (dev container + prod), reading `Ocr:TessdataPath` (defaults to the `TESSDATA_PREFIX` system path) and `Ocr:Language` (default `eng`).
  - `none` — a disabled adapter that reports OCR unavailable, so the app runs on environments without the native libs (the macOS host) and `/scan` returns a clear "not available" instead of crashing.
- Add `CoffeeLabelParser` (pure, no native deps): heuristics + regex turning raw OCR text into best-effort fields (name, roaster, origin, roast level, weight).
- Add a driving port `ICoffeeScanService` orchestrating OCR + parsing + photo retention.
- Add `POST /api/coffees/scan` (multipart, `[Authorize]`): validate the image, store it (reusing the M2 photo storage so the photo can become the coffee image), OCR it, parse it, and return `{ rawText, parsed fields, photoPath }`. It does **not** create a coffee.
- Config: `Ocr` section (`Engine`, `TessdataPath`, `Language`); `scripts/get-tessdata` stays optional/bare-metal only.

Out of scope (deferred): the camera/form UX and reusing the returned photo on save (M6); non-English language packs; alternative engines (PaddleOCR/RapidOCR — the `Ocr:Engine` switch leaves room); creating the coffee from the scan (scan only pre-fills).

## Capabilities

### New Capabilities
- `label-scan`: extracting text from a coffee-bag photo and parsing it into best-effort form fields.

## Impact

- **New package:** `TesseractOCR` (Infrastructure). Native libs (`libtesseract`/`libleptonica`) are provided by the dev container (apt) and the prod image (M7); **not present on the macOS host** — hence the `none` engine fallback.
- **Modified projects:** `CoffeeTracker.Application` (`IOcrService`/`OcrResult`, `ICoffeeScanService` + DTOs, `CoffeeLabelParser`, scan service), `CoffeeTracker.Infrastructure` (`TesseractOcrService`, `DisabledOcrService`, `OcrOptions`, DI engine selection), `CoffeeTracker.Api` (`scan` endpoint).
- **Configuration:** `Ocr:Engine` defaults to `tesseract`; `appsettings.Development.json` sets it to `none` so the host run never requires native libs. CI builds (compile only) need no native libs; OCR adapter correctness is verified in the container/manually, parser correctness by unit tests.
- **No schema change.**

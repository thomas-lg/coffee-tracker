# Design notes — M5 Snap-to-fill OCR (backend)

Decisions worth reviewing; defaults noted.

## 1. OCR behind a driven port, engine selected by config

`IOcrService` is a driven (output) port in Application: `Task<OcrResult> ReadAsync(Stream image, CancellationToken)`. `OcrResult` carries `Available` (bool), `RawText` (string), so callers distinguish "OCR ran, here's text" from "OCR not available here". The adapter is chosen at DI time by `Ocr:Engine`:

- `tesseract` (default) → `TesseractOcrService` (Infrastructure) using the `TesseractOCR` NuGet.
- `none` → `DisabledOcrService` returning `Available = false`.

This is the swap point PLAN calls out (a future PaddleOCR/RapidOCR is another `Ocr:Engine` value), and it doubles as the **graceful-degradation** mechanism.

## 2. The native-lib host problem (key decision)

The `TesseractOCR` NuGet P/Invokes native `libtesseract`/`libleptonica`. Those are installed in the **dev container** (apt) and will be in the **prod image** (M7) — but **not on the macOS host**, where the app is normally run. Loading the engine there would fail.

**Decision:** `appsettings.Development.json` sets `Ocr:Engine = none`, so the host app starts cleanly and every non-OCR endpoint works; `POST /api/coffees/scan` returns **503 Service Unavailable** ("OCR is not enabled in this environment"). Real OCR is exercised in the dev container / prod (Engine `tesseract`). To test real OCR on the host, the user can `brew install tesseract` and set `Ocr__Engine=tesseract` — documented, not required.

The Tesseract engine is created **lazily/per-request** (or cached) but never at app startup, so a misconfigured `tesseract` engine can't take the whole app down on boot — a failed `ReadAsync` returns `Available=false` + logs, surfaced as 503.

## 3. Parser is pure and separately testable

`CoffeeLabelParser` (Application, no framework/native deps) takes raw text → a `ScannedCoffeeDto` of **all-optional** best-effort fields. Heuristics: roast level by keyword match (light/medium/dark/espresso), weight by regex (`\d+\s?g|kg|oz`), origin by a country/region keyword list, roaster/name from prominent lines. It never throws on weird input — worst case all fields null. This is the unit-tested core (CI has no Tesseract, so the parser is where automated coverage lives).

## 4. Scan stores the photo for reuse

`POST /scan` reuses the M2 `IPhotoStorage` (same content-type allowlist, size cap, random filename) to persist the uploaded image and returns its relative `photoPath`. The M6 save flow can then attach that already-stored photo to the new coffee instead of re-uploading. Scan validates the upload exactly like the photo endpoint (415/413/400 on bad input). Scan does **not** create a coffee.

## 5. Endpoint shape

`POST /api/coffees/scan` (multipart `IFormFile`, `[Authorize]`) → `200 { rawText, parsed: ScannedCoffeeDto, photoPath }`. `400` empty/invalid file; `503` when OCR is the `none` engine or the engine failed. Lives on a `ScanController` (or `CoffeesController`) depending only on `ICoffeeScanService`.

## 6. Config

`Ocr:Engine` (`tesseract` | `none`, default `tesseract`); `Ocr:TessdataPath` (optional — defaults to the `TESSDATA_PREFIX`/system path, only overrides when set); `Ocr:Language` (default `eng`). `scripts/get-tessdata` remains optional and only for bare-metal-without-apt.

## 7. Testing strategy

- **Unit (CI-safe, no native libs):** `CoffeeLabelParser` against representative raw-text samples; `CoffeeScanService` against a fake `IOcrService` (available + disabled) and fake `IPhotoStorage` — asserts disabled → 503-mapped status, invalid upload → rejected, success → parsed + photoPath.
- **Manual / container:** real `TesseractOcrService` on a few real bag photos (PLAN's accuracy gauge); not run in CI.

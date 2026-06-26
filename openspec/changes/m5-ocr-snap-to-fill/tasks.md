## 1. Ports, DTOs, parser (Application)

- [ ] 1.1 Add `IOcrService` driven port + `OcrResult` (`Available`, `RawText`)
- [ ] 1.2 Add `ScannedCoffeeDto` (all-optional best-effort fields) + `ScanResponseDto` (`RawText`, `Parsed`, `PhotoPath`)
- [ ] 1.3 Add `ICoffeeScanService` driving port + typed result (Success / InvalidUpload / OcrUnavailable)
- [ ] 1.4 Add `CoffeeLabelParser` (pure heuristics + regex → ScannedCoffeeDto)
- [ ] 1.5 Implement `CoffeeScanService`: validate via IPhotoStorage save, OCR, parse, return rawText+parsed+photoPath; map disabled/failed OCR to OcrUnavailable

## 2. Infrastructure adapters

- [ ] 2.1 Add `TesseractOCR` NuGet to Infrastructure
- [ ] 2.2 Add `OcrOptions` (`Engine`, `TessdataPath`, `Language`) bound from `Ocr` section
- [ ] 2.3 Add `TesseractOcrService` (lazy engine; reads tessdata path/lang; never throws out — returns OcrResult)
- [ ] 2.4 Add `DisabledOcrService` (Available=false)
- [ ] 2.5 DI: register the OCR impl selected by `Ocr:Engine` (default tesseract; `none` → disabled)

## 3. API

- [ ] 3.1 `POST /api/coffees/scan` (multipart, `[Authorize]`) on a scan controller → 200 {rawText, parsed, photoPath} / 400 / 503

## 4. Config & docs

- [ ] 4.1 `Ocr` section in `appsettings.json` (Engine=tesseract, Language=eng); `appsettings.Development.json` Engine=none
- [ ] 4.2 README: scan endpoint + Ocr env vars (`Ocr__Engine`, `Ocr__TessdataPath`, `Ocr__Language`) + host note (brew tesseract to test locally)

## 5. Verify

- [ ] 5.1 Unit tests: `CoffeeLabelParser` (roast/weight/origin extraction, junk input → nulls) + `CoffeeScanService` (disabled → OcrUnavailable, invalid upload, success path) against fakes
- [ ] 5.2 `dotnet build` clean; `dotnet test` green (no native libs needed)
- [ ] 5.3 Host smoke: `Ocr:Engine=none` → app runs; `POST /scan` → 503; invalid upload → 400
- [ ] 5.4 (Optional, container/brew) real Tesseract: `POST /scan` with a bag photo → rawText + parsed fields; gauge accuracy

# label-scan Specification

## Purpose
TBD - created by archiving change m5-ocr-snap-to-fill. Update Purpose after archive.
## Requirements
### Requirement: A coffee-bag photo can be scanned to pre-fill fields

The system SHALL expose `POST /api/coffees/scan` accepting a multipart image upload from an authenticated user. When OCR is available, it SHALL extract the label text and return the raw text together with best-effort parsed fields (name, roaster, origin, roast level, weight) and the stored photo's relative path. The endpoint SHALL NOT create a coffee.

#### Scenario: Scanning a bag photo returns text and parsed fields

- **WHEN** an authenticated user posts a valid image to `/api/coffees/scan` and OCR is available
- **THEN** the system SHALL respond with the raw extracted text, a set of best-effort parsed fields, and the stored photo path
- **AND** SHALL NOT create a coffee

#### Scenario: Invalid upload is rejected

- **WHEN** the uploaded file is empty, too large, or not an allowed image type
- **THEN** the system SHALL reject it (4xx)
- **AND** SHALL NOT store a file

#### Scenario: Unauthenticated scan is rejected

- **WHEN** a client without a valid token posts to `/api/coffees/scan`
- **THEN** the system SHALL respond with `401`

### Requirement: OCR is an optional, swappable capability

The system SHALL select the OCR implementation by configuration (`Ocr:Engine`) so it can be swapped without code changes, and SHALL run normally when OCR is disabled or its native dependencies are absent. When OCR is unavailable, the scan endpoint SHALL respond with `503` rather than failing to start or returning a server error.

#### Scenario: App runs with OCR disabled

- **WHEN** `Ocr:Engine` is `none` (or the OCR engine cannot be initialized)
- **THEN** the application SHALL start and serve all non-scan endpoints normally
- **AND** a request to `/api/coffees/scan` SHALL respond with `503`

#### Scenario: Parsing is resilient to unrecognized text

- **WHEN** OCR returns text that matches none of the parser's field heuristics
- **THEN** the system SHALL return the raw text with parsed fields left empty
- **AND** SHALL NOT error

### Requirement: OCR work is bounded and never leaks stored photos

A single OCR run SHALL be bounded by a configurable hard timeout (`Ocr:TimeoutSeconds`, default 30 seconds): a run that exceeds it SHALL be terminated and reported as unavailable (`503`), distinct from a genuine caller cancellation. The number of OCR runs executing concurrently SHALL be capped by configuration (`Ocr:MaxConcurrency`, default twice the processor count); requests beyond the cap SHALL queue for a slot rather than spawn unbounded native processes. When a scan stores the photo before running OCR and OCR then fails or is cancelled, the system SHALL delete the stored photo so it is not orphaned.

#### Scenario: A stuck OCR run is bounded by the timeout

- **WHEN** an OCR run exceeds the configured timeout
- **THEN** the system SHALL terminate the underlying process and respond with `503`
- **AND** the scan SHALL NOT leave an orphaned stored photo

#### Scenario: Concurrent scans are admission-controlled

- **WHEN** more scans are in flight than the configured concurrency cap
- **THEN** the excess SHALL wait for a slot rather than starting additional OCR processes

#### Scenario: A cancelled scan cleans up its stored photo

- **WHEN** the caller cancels (or OCR throws) after the photo has already been stored
- **THEN** the system SHALL delete the stored photo before completing


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


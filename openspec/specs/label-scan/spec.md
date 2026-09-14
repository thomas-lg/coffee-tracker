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

The system SHALL keep the OCR implementation behind one port so it can be swapped without code changes, and SHALL run normally when OCR is disabled or its native dependencies are absent. `Ocr:Engine` names the engine a new instance starts from; which engine is in force is stored and changed at runtime (see below). When OCR is unavailable, the scan endpoint SHALL respond with `503` rather than failing to start or returning a server error.

#### Scenario: App runs with OCR disabled

- **WHEN** `Ocr:Engine` is `none` (or the OCR engine cannot be initialized)
- **THEN** the application SHALL start and serve all non-scan endpoints normally
- **AND** a request to `/api/coffees/scan` SHALL respond with `503`

#### Scenario: Parsing is resilient to unrecognized text

- **WHEN** OCR returns text that matches none of the parser's field heuristics
- **THEN** the system SHALL return the raw text with parsed fields left empty
- **AND** SHALL NOT error

### Requirement: OCR work is bounded and never leaks stored photos

A single OCR run SHALL be bounded by a configurable hard timeout (`Ocr:TimeoutSeconds`, default 30 seconds): a run that exceeds it SHALL be terminated and reported as unavailable (`503`), distinct from a genuine caller cancellation. The number of OCR runs executing concurrently SHALL be capped by configuration (`Ocr:MaxConcurrency`); requests beyond the cap SHALL queue for a slot rather than spawn unbounded native processes. When the cap is unset each engine SHALL choose its own, because what one costs to run is not what another costs. When a scan stores the photo before running OCR and OCR then fails or is cancelled, the system SHALL delete the stored photo so it is not orphaned.

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

### Requirement: An administrator chooses the OCR engine at runtime

The system SHALL persist which OCR engine scanning uses, and SHALL let an administrator
change it without restarting the instance. The change SHALL apply to the next scan.

When no engine has been chosen, the system SHALL use the one named by configuration, so
an instance that is upgraded rather than installed keeps scanning as its deployment says.

#### Scenario: Reading the engine on an instance nobody has configured

- **WHEN** an administrator reads the scanning settings and no choice has been stored
- **THEN** the system SHALL report the engine named by configuration

#### Scenario: Choosing an engine

- **WHEN** an administrator selects a different engine
- **THEN** the system SHALL store it
- **AND** the next scan SHALL use it
- **AND** no restart SHALL be required

#### Scenario: A stored value the build does not recognise

- **WHEN** the stored engine is not one the build knows
- **THEN** the system SHALL fall back to the configured engine
- **AND** SHALL NOT disable scanning

#### Scenario: Changing the engine leaves the account policy alone

- **WHEN** an administrator changes the engine
- **THEN** whether app accounts may sign in or register SHALL be unchanged

### Requirement: The available engines are reported with their availability

The system SHALL report every engine the build knows, each marked with whether it can run
on this host, so a client can refuse a choice that would answer 503 to every scan.

#### Scenario: An engine the host cannot run

- **WHEN** an engine's dependencies are absent from the host
- **THEN** the system SHALL report that engine as unavailable

### Requirement: Low-confidence lines are treated as noise

The system SHALL ignore recognised lines whose confidence falls below a floor, so
background clutter cannot become a coffee's name. The floor SHALL be reported by the
engine that produced the read rather than fixed for the pipeline, because engines differ
in how they score: the same clutter scores under 50 on one engine and around 81 on
another.

#### Scenario: Clutter is dropped

- **WHEN** a read contains lines below the reporting engine's floor
- **THEN** those lines SHALL NOT contribute to any parsed field

#### Scenario: An engine that reports no floor

- **WHEN** a read carries no floor
- **THEN** the parser SHALL apply its own default

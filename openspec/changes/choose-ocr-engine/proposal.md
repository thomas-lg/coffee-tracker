## Why

The instance now carries two OCR engines, and which one is better depends on the bags
being photographed. RapidOCR scores 83% against Tesseract's 74% on the benchmark and
finds the coffee's name 72% of the time against 58%, but it is about five times slower
per scan and Tesseract still reads roast level and weight slightly better.

The choice was configuration, read once at startup, which made it a deployment decision
for someone who cannot see the bags. It belongs to whoever runs the instance and looks
at the results.

## What Changes

- **The engine is stored** in the settings row and changed from **Admin -> Scanning**.
  The change applies on the next scan; nothing restarts.
- **`Ocr:Engine` becomes the default a new instance starts from.** The column is
  nullable and null means "whatever the deployment configured", so an upgraded instance
  keeps scanning exactly as it did.
- **The admin screen explains the choice** rather than listing three names: what each
  engine is good at, what it costs, and whether it is installed on this host. An engine
  a build does not carry cannot be selected.
- **The confidence gate moves onto `OcrResult`.** Each engine reports the floor its own
  scoring calls for (55 for Tesseract, 70 for RapidOCR), so nothing downstream has to
  know which engine produced a read. That is what makes switching at runtime safe, and
  it removes the DI plumbing that previously injected a gate.

Out of scope: per-user or per-scan engine choice, and any automatic fallback from one
engine to the other. A scan that fails answers 503 as it always has.

## Capabilities

### Modified Capabilities

- `label-scan`: the engine is a stored, administrator-controlled setting rather than
  deployment configuration, and the confidence floor travels with each read.
- `web-client`: the admin view gains a Scanning section.

## Impact

- **Migration** `AddOcrEngineSetting`: one nullable TEXT column on `AppSettings`. No data
  is rewritten, and a null keeps existing behaviour.
- **Modified projects:** `CoffeeTracker.Application` (port, DTOs, service, parser),
  `CoffeeTracker.Infrastructure` (policy adapter, switching adapter, catalogue, DI),
  `CoffeeTracker.Api` (one controller), and the Angular admin package.
- **Operator-visible:** none until an administrator chooses. `Ocr__Engine` still sets the
  starting point and is documented as such.

## Why

A self-hosted instance holds the only copy of its catalog. The photos are ordinary files
an operator can copy off the volume; the coffees and reviews are rows in a SQLite file
that is open, in WAL mode, whenever the container is up — so the obvious backup, copying
`coffee.db` out from under a running process, is the one that can hand back a torn
database. The remaining answer is to stop the container first, which nobody does on a
schedule.

There is also no way to move a catalog between instances, so a rebuild starts empty.

## What Changes

- **`GET /api/admin/backup`** returns the whole catalog — every coffee with its reviews
  and flavour tags — as a JSON document carrying a format version.
- **`POST /api/admin/backup`** restores such a document, **replacing** the catalog.
  Validation runs over the entire file before anything is written, so a bad row near the
  end cannot leave the instance holding neither the old catalog nor a whole new one.
- Both are administrator-only, and reachable from a **Backup** tab in the admin view.
- **Flavour tags travel by name.** Tag ids are per-instance, names are the stable part.
  A name the instance does not know is skipped and reported rather than created — the tag
  list is a closed set the UI renders as chips, so inventing one would put a tag on screen
  that no filter can ever match.
- **Photos are out of scope.** The file is data only. A restored coffee keeps its
  `photoPath`, which resolves if the photo volume came along and leaves a missing image if
  it did not.

Out of scope: accounts, sessions and settings. A restore writes coffees and reviews and
nothing else, so it cannot be used to grant anyone access.

## Capabilities

### New Capabilities

- `backup`: exporting and restoring the catalog.

### Modified Capabilities

- `web-client`: the admin view gains a third tab, with the two-step confirmation the
  destructive photo delete already uses.

## Impact

- **New:** `BackupDtos`, `IBackupService`/`BackupService`, `IBackupStore`/`EfBackupStore`,
  `AdminBackupController`; on the client, `AdminBackupApi`, `BackupStore`, `BackupScreen`.
- **No migration.** A restore writes through the existing model.
- **`CreatedByUserId` is carried verbatim.** A catalog restored onto an instance with
  different accounts keeps ids that resolve to nobody, so those coffees show no author and
  can be edited by no one but an administrator. Restoring onto the instance the file came
  from — the backup case — is unaffected.

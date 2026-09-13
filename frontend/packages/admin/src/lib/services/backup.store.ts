import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withProps, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { tapResponse } from '@ngrx/operators';
import { HttpErrorResponse } from '@angular/common/http';
import { exhaustMap, pipe, tap } from 'rxjs';
import { AdminBackupApi, type Backup, type ImportResult } from '@coffee-tracker/data';
import { CoffeesStore } from '@coffee-tracker/coffees';
import { ToastService } from '@coffee-tracker/ui';

/**
 * Export and restore of the catalog.
 *
 * A restore replaces everything, so the screen arms a confirmation the way photo cleanup
 * does. The chosen file is parsed in the browser before anything is sent: a file that is
 * not JSON at all should say so without a round trip, and the API would only answer 400
 * to something it cannot read either.
 */
export const BackupStore = signalStore(
  withState({
    exporting: false,
    /** The parsed file waiting for confirmation, and its name for the prompt. */
    staged: null as { backup: Backup; fileName: string } | null,
    importing: false,
    /** What the last restore wrote, so the screen can report more than "done". */
    lastResult: null as ImportResult | null,
  }),
  withProps(() => ({
    _api: inject(AdminBackupApi),
    _toast: inject(ToastService),
    _catalog: inject(CoffeesStore),
  })),
  withMethods((store) => ({
    /**
     * Downloads the export as a file. The browser cannot be handed the response as a
     * download directly because the request carries the bearer token, so the JSON is
     * fetched and turned into a blob here.
     */
    exportCatalog: rxMethod<void>(
      pipe(
        tap(() => patchState(store, { exporting: true })),
        exhaustMap(() =>
          store._api.export().pipe(
            tapResponse({
              next: (backup) => {
                patchState(store, { exporting: false });
                save(backup);
              },
              error: () => {
                patchState(store, { exporting: false });
                store._toast.show('Could not export the catalog.', 'error');
              },
            }),
          ),
        ),
      ),
    ),

    /** Reads and parses a chosen file, then waits for the user to confirm. */
    async choose(file: File): Promise<void> {
      try {
        const backup = JSON.parse(await file.text()) as Backup;
        // Enough of a shape check to tell "wrong file" from "file this instance cannot
        // read"; the API decides the latter, and says which version it found.
        if (typeof backup?.formatVersion !== 'number' || !Array.isArray(backup?.coffees)) {
          store._toast.show('That file is not a Coffee Tracker backup.', 'error');
          return;
        }
        patchState(store, { staged: { backup, fileName: file.name }, lastResult: null });
      } catch {
        store._toast.show('That file could not be read as JSON.', 'error');
      }
    },

    cancel: () => patchState(store, { staged: null }),

    /** Replaces the catalog. Only reachable once the user has confirmed. */
    confirmImport: rxMethod<void>(
      pipe(
        tap(() => patchState(store, { importing: true })),
        exhaustMap(() =>
          store._api.import(store.staged()!.backup).pipe(
            tapResponse({
              next: (result) => {
                patchState(store, { importing: false, staged: null, lastResult: result });
                store._catalog.reload(); // the shelf is a different shelf now
                // Short: the counts and any skipped tags are on screen in the panel
                // below, which stays put after the toast has gone.
                store._toast.show('Catalog restored.', 'success');
              },
              error: (err: unknown) => {
                patchState(store, { importing: false, staged: null });
                // The API's reason is the useful one — it names the format version it
                // found, or the row it refused.
                store._toast.show(detailOf(err) ?? 'The restore was refused.', 'error');
              },
            }),
          ),
        ),
      ),
    ),
  })),
);

/** Hands the export to the browser as a download. */
function save(backup: Backup): void {
  const url = URL.createObjectURL(
    new Blob([JSON.stringify(backup, null, 2)], { type: 'application/json' }),
  );
  const link = document.createElement('a');
  link.href = url;
  link.download = `coffee-tracker-${backup.exportedAt.slice(0, 10)}.json`;
  link.click();
  // Revoked on the next tick: revoking synchronously can beat the download starting.
  setTimeout(() => URL.revokeObjectURL(url));
}

function detailOf(err: unknown): string | null {
  return err instanceof HttpErrorResponse
    ? ((err.error as { detail?: string } | null)?.detail ?? null)
    : null;
}

export type BackupStore = InstanceType<typeof BackupStore>;

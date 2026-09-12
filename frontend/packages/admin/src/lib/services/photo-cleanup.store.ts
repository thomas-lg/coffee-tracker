import { computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { patchState, signalStore, withComputed, withMethods, withProps, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { extendResource, withValueOnError } from '@ngrx/signals/resource';
import { tapResponse } from '@ngrx/operators';
import { pipe, switchMap, tap } from 'rxjs';
import {
  setFulfilled,
  setPending,
  setRequestError,
  withRequestStatus,
} from '@coffee-tracker/util';
import { AdminPhotosApi, type PhotoDeleteResult, type PhotoListItem } from '@coffee-tracker/data';

export type PhotoFilter = 'all' | 'unused';

/** Outcome of the last delete. The screen turns this into a toast, then acknowledges it. */
export type DeleteOutcome = { kind: 'ok'; result: PhotoDeleteResult } | { kind: 'error' };

type PhotoCleanupState = {
  filter: PhotoFilter;
  /** Selected paths. Only unused photos are ever added (used ones aren't selectable). */
  selection: readonly string[];
  lastOutcome: DeleteOutcome | null;
};

/**
 * Page-scoped store (provided by the component, not root) for the admin photo-cleanup
 * screen. No `providedIn`, so it stays in the component's `providers`.
 *
 * `selection` is an array rather than a Set: patchState deep-freezes state in dev and
 * builds deep signals from record-shaped slices, and a Set sits awkwardly in both. The
 * derived `selectionSet` keeps `isSelected` O(1) inside the template's @for.
 */
export const PhotoCleanupStore = signalStore(
  withState<PhotoCleanupState>({ filter: 'all', selection: [], lastOutcome: null }),
  withRequestStatus(),
  withProps(() => {
    const api = inject(AdminPhotosApi);
    return {
      _api: api,
      // See CoffeesStore: a resource's value() throws while errored, and withValueOnError
      // answers the empty default instead.
      _list: extendResource(
        rxResource({ stream: () => api.list(), defaultValue: [] as PhotoListItem[] }),
        withValueOnError([]),
      ),
    };
  }),
  withComputed(({ _list, filter, selection }) => {
    const photos = computed(() => _list.value());
    return {
      photos,
      selectionSet: computed(() => new Set(selection())),
      loading: _list.isLoading,
      error: computed(() => (_list.error() ? 'Could not load stored photos.' : null)),
      storedCount: computed(() => photos().length),
      unusedCount: computed(() => photos().filter((p) => !p.used).length),
      selectedCount: computed(() => selection().length),
      visible: computed(() =>
        filter() === 'unused' ? photos().filter((p) => !p.used) : photos(),
      ),
    };
  }),
  withMethods((store) => ({
    isSelected(path: string): boolean {
      return store.selectionSet().has(path);
    },

    toggle(path: string): void {
      const current = store.selection();
      patchState(store, {
        selection: current.includes(path)
          ? current.filter((p) => p !== path)
          : [...current, path],
      });
    },

    selectAllUnused(): void {
      patchState(store, {
        selection: store.photos().filter((p) => !p.used).map((p) => p.path),
      });
    },

    clearSelection(): void {
      patchState(store, { selection: [] });
    },

    setFilter(value: PhotoFilter): void {
      patchState(store, { filter: value });
    },

    /** Clears the one-shot outcome once the screen has shown it. */
    acknowledgeOutcome(): void {
      patchState(store, { lastOutcome: null, requestStatus: 'idle' });
    },

    /**
     * Deletes the current selection, clears it, and refetches the list.
     *
     * switchMap rather than concatMap: the screen arms a confirmation and disables the
     * button while `pending()`, so a second delete cannot overlap — and if one somehow
     * did, abandoning the stale request is the right answer.
     */
    deleteSelected: rxMethod<void>(
      pipe(
        tap(() => patchState(store, setPending(), { lastOutcome: null })),
        switchMap(() =>
          store._api.delete([...store.selection()]).pipe(
            tapResponse({
              next: (result) => {
                patchState(store, setFulfilled(), {
                  selection: [],
                  lastOutcome: { kind: 'ok', result },
                });
                store._list.reload();
              },
              error: () =>
                patchState(store, setRequestError('Delete failed — please retry.'), {
                  lastOutcome: { kind: 'error' },
                }),
            }),
          ),
        ),
      ),
    ),
  })),
);

export type PhotoCleanupStore = InstanceType<typeof PhotoCleanupStore>;

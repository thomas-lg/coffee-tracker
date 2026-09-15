import { computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import {
  patchState,
  signalStore,
  withComputed,
  withMethods,
  withProps,
  withState,
} from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { extendResource, withValueOnError } from '@ngrx/signals/resource';
import { tapResponse } from '@ngrx/operators';
import { pipe, switchMap } from 'rxjs';
import { createActionState, trackAction } from '@coffee-tracker/util';
import { ToastService } from '@coffee-tracker/ui';
import { AdminPhotosApi, type PhotoListItem } from '@coffee-tracker/data';

export type PhotoFilter = 'all' | 'unused';

type PhotoCleanupState = {
  filter: PhotoFilter;
  /** Selected paths. Only unused photos are ever added (used ones aren't selectable). */
  selection: readonly string[];
};

/**
 * `selection` is an array rather than a Set: patchState deep-freezes state in dev and
 * builds deep signals from record-shaped slices, and a Set sits awkwardly in both. The
 * derived `selectionSet` keeps `isSelected` O(1) inside the template's @for.
 */
export const PhotoCleanupStore = signalStore(
  withState<PhotoCleanupState>({ filter: 'all', selection: [] }),
  withProps(() => {
    const api = inject(AdminPhotosApi);
    return {
      // Its own state rather than a store-wide status: this one feeds a control, and a
      // status that never fades would still read as "done" on the next confirm row.
      deleteAction: createActionState(),

      _api: api,
      _toast: inject(ToastService),
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
      visible: computed(() => (filter() === 'unused' ? photos().filter((p) => !p.used) : photos())),
    };
  }),
  withMethods((store) => ({
    isSelected(path: string): boolean {
      return store.selectionSet().has(path);
    },

    toggle(path: string): void {
      const current = store.selection();
      patchState(store, {
        selection: current.includes(path) ? current.filter((p) => p !== path) : [...current, path],
      });
    },

    selectAllUnused(): void {
      patchState(store, {
        selection: store
          .photos()
          .filter((p) => !p.used)
          .map((p) => p.path),
      });
    },

    clearSelection(): void {
      patchState(store, { selection: [] });
    },

    setFilter(value: PhotoFilter): void {
      patchState(store, { filter: value });
    },

    /**
     * switchMap rather than concatMap: ct-confirm-action makes this a confirmed action
     * and stops responding while it runs, so a second delete cannot overlap, and if one
     * somehow did, abandoning the stale request is the right answer.
     */
    deleteSelected: rxMethod<void>(
      pipe(
        switchMap(() =>
          store._api.delete([...store.selection()]).pipe(
            trackAction(store.deleteAction),
            tapResponse({
              next: (result) => {
                patchState(store, { selection: [] });
                store._list.reload();
                store._toast.show(
                  `Deleted ${result.deleted}, skipped ${result.skipped}`,
                  'success',
                );
              },
              error: () => store._toast.show('Delete failed. Please retry.', 'error'),
            }),
          ),
        ),
      ),
    ),
  })),
);

export type PhotoCleanupStore = InstanceType<typeof PhotoCleanupStore>;

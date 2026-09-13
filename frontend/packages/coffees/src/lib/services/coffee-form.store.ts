import { inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { patchState, signalStore, withMethods, withProps, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { tapResponse } from '@ngrx/operators';
import { catchError, exhaustMap, filter, map, of, pipe, switchMap, tap } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { ToastService } from '@coffee-tracker/ui';
import { CoffeesApi, ScanApi, type CoffeeCreate, type RoastLevel } from '@coffee-tracker/data';
import { CoffeesStore } from './coffees.store';
import { roastBucket } from '../utils/coffee-visual';

/** Flat, all-required editable shape (Signal Forms binds cleanly to non-optional fields). */
export interface CoffeeFormModel {
  name: string;
  roaster: string;
  origin: string;
  roastLevel: RoastLevel | '';
  price: number;
  dateBought: string;
  shopName: string;
  purchaseUrl: string;
}

export function today(): string {
  return new Date().toISOString().slice(0, 10);
}

const emptyModel = (): CoffeeFormModel => ({
  name: '',
  roaster: '',
  origin: '',
  roastLevel: '',
  price: 0,
  dateBought: today(),
  shopName: '',
  purchaseUrl: '',
});

function toDto(m: CoffeeFormModel): CoffeeCreate {
  return {
    name: m.name,
    roaster: m.roaster,
    origin: m.origin,
    // required() guarantees a non-empty selection before submit.
    roastLevel: m.roastLevel as RoastLevel,
    price: m.price,
    dateBought: m.dateBought,
    shopName: m.shopName || null,
    purchaseUrl: m.purchaseUrl || null,
  };
}

export const CoffeeFormStore = signalStore(
  withState({
    loading: false,
    scanning: false,
    submitting: false,
    /** Photo already attached to the coffee being edited; the screen previews it. */
    photoUrl: null as string | null,
  }),
  withProps(() => ({
    _api: inject(CoffeesApi),
    _scanApi: inject(ScanApi),
    _router: inject(Router),
    _toast: inject(ToastService),
    _catalog: inject(CoffeesStore),
    /**
     * The form's value. A plain writable signal in withProps rather than withState,
     * because Signal Forms binds two-way and writes back into it — patchState-managed
     * state is readonly and cannot be a form model.
     */
    model: signal<CoffeeFormModel>(emptyModel()),
  })),
  withMethods((store) => ({
    /**
     * Fed a signal, so it re-runs when the route id changes. switchMap then abandons a
     * load still in flight, which the previous effect-plus-await could not — two quick
     * edits raced and the slower response won.
     */
    load: rxMethod<number | null>(
      pipe(
        filter((id): id is number => id != null),
        tap(() => patchState(store, { loading: true })),
        switchMap((id) =>
          store._api.get(id).pipe(
            tapResponse({
              next: (c) => {
                patchState(store, { loading: false, photoUrl: c.photoUrl ?? null });
                store.model.set({
                  name: c.name,
                  roaster: c.roaster,
                  origin: c.origin,
                  roastLevel: c.roastLevel,
                  price: c.price,
                  dateBought: c.dateBought,
                  shopName: c.shopName ?? '',
                  purchaseUrl: c.purchaseUrl ?? '',
                });
              },
              // Not finalize: switchMap unsubscribes the previous request only after the
              // outer tap has set loading, so a finalize would immediately clear the flag
              // for the request that just started.
              error: () => {
                patchState(store, { loading: false });
                store._toast.show('Could not load that coffee.', 'error');
              },
            }),
          ),
        ),
      ),
    ),

    /** Snap-to-fill: read the bag and pre-fill whatever the scan recognised. */
    scan: rxMethod<File>(
      pipe(
        tap(() => patchState(store, { scanning: true })),
        switchMap((file) =>
          store._scanApi.scan(file).pipe(
            tapResponse({
              next: ({ parsed }) => {
                store.model.update((m) => ({
                  ...m,
                  name: parsed.name ?? m.name,
                  roaster: parsed.roaster ?? m.roaster,
                  origin: parsed.origin ?? m.origin,
                  // OCR returns free text (e.g. "medium-dark"); map it onto the enum.
                  roastLevel: parsed.roastLevel ? roastBucket(parsed.roastLevel) : m.roastLevel,
                }));
                patchState(store, { scanning: false });
                store._toast.show(
                  'Bag scanned — fields pre-filled. Check them before saving.',
                  'success',
                );
              },
              error: (err: unknown) => {
                patchState(store, { scanning: false });
                const off = err instanceof HttpErrorResponse && err.status === 503;
                store._toast.show(
                  off
                    ? 'OCR is off on this host — fill the form in manually.'
                    : 'Could not read that photo.',
                  off ? 'info' : 'error',
                );
              },
            }),
          ),
        ),
      ),
    ),

    /**
     * exhaustMap, not switchMap: a second submit while one is in flight must be ignored,
     * not cancel-and-restart, or a double click creates two coffees.
     *
     * The photo failure is caught into a flag rather than left to reject. The coffee is
     * already saved at that point, so surfacing it as a failure would invite a resubmit
     * and a duplicate — only the save itself may keep the user on the form.
     */
    save: rxMethod<{ id: number | null; file: File | null }>(
      pipe(
        tap(() => patchState(store, { submitting: true })),
        exhaustMap(({ id, file }) => {
          const dto = toDto(store.model());
          const saved$ =
            id != null
              ? store._api.update(id, dto).pipe(map(() => id))
              : store._api.create(dto).pipe(map((c) => c.id));

          return saved$.pipe(
            switchMap((savedId) =>
              file
                ? store._api.uploadPhoto(savedId, file).pipe(
                    map(() => ({ savedId, photoFailed: false })),
                    catchError(() => of({ savedId, photoFailed: true })),
                  )
                : of({ savedId, photoFailed: false }),
            ),
            tapResponse({
              next: ({ savedId, photoFailed }) => {
                store._catalog.reload(); // keep the grid in sync with the new/edited coffee
                store._toast.show(
                  photoFailed
                    ? 'Coffee saved, but the photo failed to upload. You can retry it from Edit.'
                    : id != null
                      ? 'Coffee updated.'
                      : 'Coffee added.',
                  photoFailed ? 'error' : 'success',
                );
                void store._router.navigate(['/coffees', savedId]);
              },
              error: () => store._toast.show('Could not save the coffee.', 'error'),
              finalize: () => patchState(store, { submitting: false }),
            }),
          );
        }),
      ),
    ),
  })),
);

export type CoffeeFormStore = InstanceType<typeof CoffeeFormStore>;

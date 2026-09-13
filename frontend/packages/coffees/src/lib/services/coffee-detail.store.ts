import { computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { patchState, signalStore, withComputed, withMethods, withProps, withState } from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { tapResponse } from '@ngrx/operators';
import { exhaustMap, pipe, tap } from 'rxjs';
import { AuthStore } from '@coffee-tracker/auth';
import { CoffeesApi, FlavorTagsApi, ReviewsApi } from '@coffee-tracker/data';
import { ToastService } from '@coffee-tracker/ui';
import { CoffeesStore } from './coffees.store';

/**
 * Everything the coffee detail screen does apart from render it: three reads keyed on
 * the route id, the rate-today form, and the armed-confirm delete.
 *
 * Component-provided rather than root, like CoffeeFormStore — the state belongs to one
 * screen and should die with it, which also means two tabs on different coffees do not
 * share a half-typed rating.
 */
export const CoffeeDetailStore = signalStore(
  withState({
    coffeeId: null as number | null,

    // Rate today. Plain state rather than a form model: the template binds these one at
    // a time, so there is nothing for Signal Forms to own.
    rating: 0,
    stage: '',
    notes: '',
    selectedTags: new Set<number>(),
    saving: false,

    confirmingDelete: false,
    deleting: false,
  }),
  withProps((store) => {
    const coffeesApi = inject(CoffeesApi);
    const reviewsApi = inject(ReviewsApi);
    const tagsApi = inject(FlavorTagsApi);
    return {
      _coffeesApi: coffeesApi,
      _reviewsApi: reviewsApi,
      _auth: inject(AuthStore),
      _toast: inject(ToastService),
      _router: inject(Router),
      _catalog: inject(CoffeesStore),

      // Keyed on the id so switching between coffees refetches rather than showing the
      // previous one's data under the new route.
      _coffee: rxResource({
        params: () => store.coffeeId() ?? undefined,
        stream: ({ params }) => coffeesApi.get(params),
      }),
      _reviews: rxResource({
        params: () => store.coffeeId() ?? undefined,
        stream: ({ params }) => reviewsApi.listForCoffee(params),
        defaultValue: [],
      }),
      _tags: rxResource({ stream: () => tagsApi.list(), defaultValue: [] }),
    };
  }),
  withComputed(({ _coffee, _reviews, _tags, _auth, coffeeId }) => {
    // A resource's value() THROWS while it is in an error state, so every read is
    // guarded — same reason as CoffeesStore. A 404 has to render "not found", not
    // blow up mid-render.
    const coffee = computed(() => (_coffee.error() ? undefined : _coffee.value()));
    const reviews = computed(() => (_reviews.error() ? [] : _reviews.value()));
    const mine = computed(() => _auth.session()?.userId ?? null);

    return {
      coffee,
      reviews,
      tags: computed(() => (_tags.error() ? [] : _tags.value())),

      // Keyed on the id too: without it, navigating from one coffee to another keeps
      // rendering the previous one's name and specs while the next request is in
      // flight, instead of showing the skeleton.
      loading: computed(() => _coffee.isLoading() && coffee()?.id !== coffeeId()),
      notFound: computed(() => !_coffee.isLoading() && !coffee()),

      myReviews: computed(() => {
        const me = mine();
        return me ? reviews().filter((r) => r.userId === me) : [];
      }),
      otherReviews: computed(() => {
        const me = mine();
        return reviews().filter((r) => r.userId !== me);
      }),
    };
  }),
  withMethods((store) => {
    const resetForm = (): void =>
      patchState(store, { rating: 0, stage: '', notes: '', selectedTags: new Set<number>() });

    return {
      /**
       * Fed the screen's id signal, not its value. The router reuses this component
       * when only the parameter changes (/coffees/7 → /coffees/8, one route config),
       * so anything that reads the id once — a store onInit, an ngOnInit — would leave
       * the screen showing the previous coffee. rxMethod re-emits when the signal does,
       * which is the same shape CoffeeFormStore.load already uses.
       */
      setCoffeeId: rxMethod<number>(
        pipe(tap((coffeeId) => patchState(store, { coffeeId }))),
      ),

      setRating: (rating: number) => patchState(store, { rating }),
      setStage: (stage: string) => patchState(store, { stage }),
      setNotes: (notes: string) => patchState(store, { notes }),

      isTagOn: (id: number): boolean => store.selectedTags().has(id),

      toggleTag(id: number): void {
        const next = new Set(store.selectedTags());
        if (!next.delete(id)) {
          next.add(id);
        }
        patchState(store, { selectedTags: next });
      },

      /**
       * exhaustMap, not switchMap: a double-clicked Save must not post two ratings, and
       * unlike a load there is nothing to abandon — the first request is the real one.
       */
      rate: rxMethod<void>(
        pipe(
          tap(() => patchState(store, { saving: true })),
          exhaustMap(() =>
            store._reviewsApi
              .create(store.coffeeId()!, {
                rating: store.rating(),
                stage: store.stage() || null,
                tastingNotes: store.notes() || null,
                tagIds: [...store.selectedTags()],
              })
              .pipe(
                tapResponse({
                  next: () => {
                    patchState(store, { saving: false });
                    resetForm();
                    store._toast.show('Rating saved for today.', 'success');
                    store._reviews.reload();
                    store._coffee.reload(); // this coffee's average moved
                    store._catalog.reload(); // and so did the one on its shelf card
                  },
                  error: () => {
                    patchState(store, { saving: false });
                    store._toast.show('Could not save your rating.', 'error');
                  },
                }),
              ),
          ),
        ),
      ),

      armDelete: () => patchState(store, { confirmingDelete: true }),
      cancelDelete: () => patchState(store, { confirmingDelete: false }),

      confirmDelete: rxMethod<void>(
        pipe(
          tap(() => patchState(store, { deleting: true })),
          exhaustMap(() =>
            store._coffeesApi.delete(store.coffeeId()!).pipe(
              tapResponse({
                next: () => {
                  patchState(store, { deleting: false, confirmingDelete: false });
                  store._catalog.reload(); // a delete changes the shelf
                  store._toast.show('Coffee deleted.', 'success');
                  void store._router.navigate(['/coffees']);
                },
                error: () => {
                  patchState(store, { deleting: false, confirmingDelete: false });
                  store._toast.show(
                    'Could not delete it (you may not have permission).',
                    'error',
                  );
                },
              }),
            ),
          ),
        ),
      ),
    };
  }),
);

export type CoffeeDetailStore = InstanceType<typeof CoffeeDetailStore>;

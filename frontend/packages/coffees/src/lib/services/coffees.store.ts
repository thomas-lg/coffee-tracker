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
import { extendResource, withValueOnError } from '@ngrx/signals/resource';
import { AuthStore } from '@coffee-tracker/auth';
import { CoffeesApi, type Coffee } from '@coffee-tracker/data';
import { roastBucket } from '@coffees/utils/coffee-visual';

export type RoastFilter = 'all' | 'Light' | 'Medium' | 'Dark';
export type CoffeeSort = 'new' | 'rating' | 'name';

type CoffeesFilters = {
  search: string;
  roast: RoastFilter;
  origin: string;
  flavor: string;
  sort: CoffeeSort;
};

const initialFilters: CoffeesFilters = {
  search: '',
  roast: 'all',
  origin: 'all',
  flavor: 'all',
  sort: 'new',
};

export const CoffeesStore = signalStore(
  { providedIn: 'root' },
  withState(initialFilters),
  withProps(() => {
    const api = inject(CoffeesApi);
    const auth = inject(AuthStore);
    return {
      // A resource's value() THROWS while it is in an error state, even with a default.
      // withValueOnError makes it answer [] instead, so every derived signal and every
      // template consumer below is safe without re-implementing the guard.
      _list: extendResource(
        rxResource({
          // Keyed on who is asking, not because the catalog differs per user, it is
          // shared, but because this store is providedIn: 'root' and signing out is a
          // client-side navigation. Without a key the instance outlives the session and
          // the next person to sign in reads the previous one's snapshot: coffees added
          // since are missing, and every photo URL in it is a signed URL that has since
          // expired, so the images 401. Undefined while signed out keeps the resource
          // idle rather than firing an unauthenticated request from the login screen.
          params: () => auth.session()?.userId,
          stream: () => api.list(),
          defaultValue: [] as Coffee[],
        }),
        withValueOnError([]),
      ),
    };
  }),
  withComputed(({ _list, search, roast, origin, flavor, sort }) => {
    const coffees = computed(() => _list.value());
    return {
      coffees,
      loading: _list.isLoading,
      error: computed(() => (_list.error() ? 'Could not load your coffees.' : null)),

      /** Distinct filter/autocomplete options derived from the loaded shelf. */
      origins: computed(() => [...new Set(coffees().map((c) => c.origin))].sort()),
      flavors: computed(() => [...new Set(coffees().flatMap((c) => c.flavorTags))].sort()),
      roasters: computed(() =>
        [
          ...new Set(
            coffees()
              .map((c) => c.roaster)
              .filter(Boolean),
          ),
        ].sort(),
      ),
      shops: computed(() =>
        [
          ...new Set(
            coffees()
              .map((c) => c.shopName)
              .filter((s): s is string => !!s),
          ),
        ].sort(),
      ),

      filtered: computed(() => {
        const q = search().toLowerCase().trim();
        const byRoast = roast();
        const byOrigin = origin();
        const byFlavor = flavor();
        let list = coffees().filter(
          (c) =>
            (byRoast === 'all' || roastBucket(c.roastLevel) === byRoast) &&
            (byOrigin === 'all' || c.origin === byOrigin) &&
            (byFlavor === 'all' || c.flavorTags.includes(byFlavor)) &&
            `${c.name} ${c.roaster} ${c.origin}`.toLowerCase().includes(q),
        );
        switch (sort()) {
          case 'rating':
            list = [...list].sort((a, b) => (b.averageRating ?? 0) - (a.averageRating ?? 0));
            break;
          case 'name':
            list = [...list].sort((a, b) => a.name.localeCompare(b.name));
            break;
          // 'new' keeps the API order (newest first).
        }
        return list;
      }),
    };
  }),
  withMethods((store) => ({
    /** Refetch the list (e.g. after add/delete elsewhere). */
    reload(): void {
      store._list.reload();
    },
    setSearch(value: string): void {
      patchState(store, { search: value });
    },
    setRoast(value: RoastFilter): void {
      patchState(store, { roast: value });
    },
    setOrigin(value: string): void {
      patchState(store, { origin: value });
    },
    setFlavor(value: string): void {
      patchState(store, { flavor: value });
    },
    /** Narrows a raw <select> value to the sort union (type-safe, no `$any`). */
    setSort(value: string): void {
      const allowed: readonly CoffeeSort[] = ['new', 'rating', 'name'];
      patchState(store, {
        sort: (allowed as readonly string[]).includes(value) ? (value as CoffeeSort) : 'new',
      });
    },
  })),
);

// `signalStore()` returns a value, so the name alone cannot annotate a variable. This
// companion type lets consumers and specs write `store: CoffeesStore` as before.
export type CoffeesStore = InstanceType<typeof CoffeesStore>;

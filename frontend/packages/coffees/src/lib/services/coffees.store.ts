import { Injectable, computed, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import type { Coffee } from '@coffee-tracker/data';
import { roastBucket } from '../utils/coffee-visual';

export type RoastFilter = 'all' | 'Light' | 'Medium' | 'Dark';
export type CoffeeSort = 'new' | 'rating' | 'name';

/**
 * Catalog store. The list is an `httpResource` (auto-fetches, reactive, refetchable);
 * search/roast/sort are plain signals composed into `filtered`.
 */
@Injectable({ providedIn: 'root' })
export class CoffeesStore {
  private readonly resource = httpResource<Coffee[]>(() => '/api/coffees', { defaultValue: [] });

  // httpResource.value THROWS while the resource is in an error state (even with a
  // defaultValue), so guard every read here — it returns the empty default on error.
  // This keeps the derived signals below (filtered/origins/…) and every template
  // consumer (grid, home) safe without each one re-implementing the guard.
  readonly coffees = computed(() => (this.resource.error() ? [] : this.resource.value()));
  readonly loading = this.resource.isLoading;
  readonly error = computed(() => (this.resource.error() ? 'Could not load your coffees.' : null));

  readonly search = signal('');
  readonly roast = signal<RoastFilter>('all');
  readonly origin = signal('all');
  readonly flavor = signal('all');
  readonly sort = signal<CoffeeSort>('new');

  /** Distinct filter/autocomplete options derived from the loaded shelf. */
  readonly origins = computed(() => [...new Set(this.coffees().map((c) => c.origin))].sort());
  readonly flavors = computed(() =>
    [...new Set(this.coffees().flatMap((c) => c.flavorTags))].sort(),
  );
  readonly roasters = computed(() =>
    [...new Set(this.coffees().map((c) => c.roaster).filter(Boolean))].sort(),
  );
  readonly shops = computed(() =>
    [...new Set(this.coffees().map((c) => c.shopName).filter((s): s is string => !!s))].sort(),
  );

  readonly filtered = computed(() => {
    const q = this.search().toLowerCase().trim();
    const roast = this.roast();
    const origin = this.origin();
    const flavor = this.flavor();
    let list = this.coffees().filter(
      (c) =>
        (roast === 'all' || roastBucket(c.roastLevel) === roast) &&
        (origin === 'all' || c.origin === origin) &&
        (flavor === 'all' || c.flavorTags.includes(flavor)) &&
        `${c.name} ${c.roaster} ${c.origin}`.toLowerCase().includes(q),
    );
    switch (this.sort()) {
      case 'rating':
        list = [...list].sort((a, b) => (b.averageRating ?? 0) - (a.averageRating ?? 0));
        break;
      case 'name':
        list = [...list].sort((a, b) => a.name.localeCompare(b.name));
        break;
      // 'new' keeps the API order (newest first).
    }
    return list;
  });

  /** Refetch the list (e.g. after add/delete elsewhere). */
  reload(): void {
    this.resource.reload();
  }

  /** Narrows a raw <select> value to the sort union (type-safe, no `$any`). */
  setSort(value: string): void {
    const allowed: readonly CoffeeSort[] = ['new', 'rating', 'name'];
    this.sort.set((allowed as readonly string[]).includes(value) ? (value as CoffeeSort) : 'new');
  }
}

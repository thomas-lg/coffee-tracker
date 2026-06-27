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

  readonly coffees = this.resource.value;
  readonly loading = this.resource.isLoading;
  readonly error = computed(() => (this.resource.error() ? 'Could not load your coffees.' : null));

  readonly search = signal('');
  readonly roast = signal<RoastFilter>('all');
  readonly sort = signal<CoffeeSort>('new');

  readonly filtered = computed(() => {
    const q = this.search().toLowerCase().trim();
    const roast = this.roast();
    let list = this.coffees().filter(
      (c) =>
        (roast === 'all' || roastBucket(c.roastLevel) === roast) &&
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
}

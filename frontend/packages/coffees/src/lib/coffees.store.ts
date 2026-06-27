import { Injectable, computed, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { CoffeesApi, type Coffee } from '@coffee-tracker/data';
import { roastBucket } from './coffee-visual';

export type RoastFilter = 'all' | 'Light' | 'Medium' | 'Dark';
export type CoffeeSort = 'new' | 'rating' | 'name';

/**
 * Signal-based catalog store (native signals — same surface a SignalStore would
 * expose; see the deferred-upgrade note). Holds the list + search/filter/sort.
 */
@Injectable({ providedIn: 'root' })
export class CoffeesStore {
  private readonly api = inject(CoffeesApi);

  readonly coffees = signal<Coffee[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

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

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      this.coffees.set(await firstValueFrom(this.api.list()));
    } catch {
      this.error.set('Could not load your coffees.');
    } finally {
      this.loading.set(false);
    }
  }
}

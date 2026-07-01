import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Button, Icon, Select } from '@coffee-tracker/ui';
import { ROAST_LEVELS } from '@coffee-tracker/data';
import { CoffeesStore, type RoastFilter } from '../services/coffees.store';
import { CoffeeCard } from './coffee-card';
import { CoffeeCardSkeleton } from './coffee-card-skeleton';

@Component({
  selector: 'ct-coffee-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, Icon, Select, CoffeeCard, CoffeeCardSkeleton],
  templateUrl: './coffee-grid.html',
})
export class CoffeeGrid {
  protected readonly store = inject(CoffeesStore);
  protected readonly roasts: RoastFilter[] = ['all', ...ROAST_LEVELS];
  protected readonly skeletons = Array.from({ length: 6 });
  // The store's list is an httpResource — it fetches on first injection, no manual load.
}

import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Button, Icon, Skeleton } from '@coffee-tracker/ui';
import { CoffeesStore, type RoastFilter } from '../services/coffees.store';
import { CoffeeCard } from './coffee-card';

@Component({
  selector: 'ct-coffee-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, Icon, Skeleton, CoffeeCard],
  templateUrl: './coffee-grid.html',
})
export class CoffeeGrid {
  protected readonly store = inject(CoffeesStore);
  protected readonly roasts: RoastFilter[] = ['all', 'Light', 'Medium', 'Dark'];
  protected readonly skeletons = Array.from({ length: 6 });
  // The store's list is an httpResource — it fetches on first injection, no manual load.
}

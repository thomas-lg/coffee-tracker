import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Button, Select } from '@coffee-tracker/ui';
import { LucideSearch } from '@lucide/angular';
import { ROAST_LEVELS } from '@coffee-tracker/data';
import { CoffeesStore, type RoastFilter } from '@coffees/services/coffees.store';
import { CoffeeCard } from '@coffees/components/coffee-card/coffee-card';
import { CoffeeShelfStates } from '@coffees/components/coffee-shelf-states/coffee-shelf-states';

@Component({
  selector: 'ct-coffee-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, LucideSearch, Select, CoffeeCard, CoffeeShelfStates],
  templateUrl: './coffee-grid.html',
})
export class CoffeeGrid {
  protected readonly store = inject(CoffeesStore);
  protected readonly roasts: RoastFilter[] = ['all', ...ROAST_LEVELS];
}

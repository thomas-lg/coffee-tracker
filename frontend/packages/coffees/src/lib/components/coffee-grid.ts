import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button, Card, Rating, Skeleton } from '@coffee-tracker/ui';
import { formatRating } from '@coffee-tracker/util';
import { CoffeesStore, type RoastFilter } from '../services/coffees.store';
import { roastGradient } from '../utils/coffee-visual';

@Component({
  selector: 'ct-coffee-grid',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Button, Card, Rating, Skeleton],
  templateUrl: './coffee-grid.html',
})
export class CoffeeGrid {
  protected readonly store = inject(CoffeesStore);
  protected readonly roastGradient = roastGradient;
  protected readonly formatRating = formatRating;
  protected readonly roasts: RoastFilter[] = ['all', 'Light', 'Medium', 'Dark'];
  protected readonly skeletons = Array.from({ length: 6 });
  // The store's list is an httpResource — it fetches on first injection, no manual load.
}

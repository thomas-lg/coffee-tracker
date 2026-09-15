import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  Button,
  ConfirmAction,
  ImageLightbox,
  Rating,
  Skeleton,
  TagChip,
} from '@coffee-tracker/ui';
import { formatDate, formatPrice, formatRating } from '@coffee-tracker/util';
import { roastGradient } from '@coffees/utils/coffee-visual';
import { CoffeeDetailStore } from './coffee-detail.store';
import { BeanScene } from '@coffees/components/bean-scene/bean-scene';

@Component({
  selector: 'ct-coffee-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Button, ConfirmAction, Rating, TagChip, Skeleton, BeanScene, ImageLightbox],
  providers: [CoffeeDetailStore],
  templateUrl: './coffee-detail.html',
})
export class CoffeeDetail {
  protected readonly store = inject(CoffeeDetailStore);

  readonly id = input.required<string>();

  protected readonly roastGradient = roastGradient;
  protected readonly formatRating = formatRating;
  protected readonly formatPrice = formatPrice;
  protected readonly formatDate = formatDate;

  /** The spec table's rows, in display order, presentation, so it stays here. */
  protected readonly specs = computed<[string, string][]>(() => {
    const c = this.store.coffee();
    if (!c) return [];
    const rows: [string, string][] = [
      ['Origin', c.origin],
      ['Roaster', c.roaster],
      ['Roast', c.roastLevel],
      ['Price', formatPrice(c.price)],
      ['Bought', formatDate(c.dateBought)],
    ];
    if (c.shopName) rows.push(['Shop', c.shopName]);
    return rows;
  });

  /** The route parameter arrives as a string; the store reads by number. */
  private readonly coffeeId = computed(() => Number(this.id()));

  constructor() {
    // The signal, not its value: the router reuses this component when only the
    // parameter changes, so the store has to keep hearing about it.
    this.store.setCoffeeId(this.coffeeId);
  }
}

import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button, CountUp, Icon, Skeleton } from '@coffee-tracker/ui';
import { BeanScene, CoffeeCard, CoffeesStore } from '@coffee-tracker/coffees';

@Component({
  selector: 'app-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Button, CountUp, Icon, Skeleton, BeanScene, CoffeeCard],
  templateUrl: './home.html',
})
export class Home {
  protected readonly store = inject(CoffeesStore);
  /** The most recent few bags for the "Fresh on the shelf" teaser. */
  protected readonly recent = computed(() => this.store.coffees().slice(0, 4));
  protected readonly skeletons = Array.from({ length: 4 });

  /** Headline numbers for the hero stat strip. */
  protected readonly stats = computed(() => {
    const list = this.store.coffees();
    const rated = list.filter((c) => c.reviewCount > 0 && c.averageRating != null);
    const avg = rated.length
      ? rated.reduce((sum, c) => sum + (c.averageRating ?? 0), 0) / rated.length
      : 0;
    return {
      bags: list.length,
      reviews: list.reduce((sum, c) => sum + (c.reviewCount ?? 0), 0),
      avg,
      origins: new Set(list.map((c) => c.origin)).size,
    };
  });
}

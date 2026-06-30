import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button, CountUp, Icon } from '@coffee-tracker/ui';
import { BeanScene, CoffeeCard, CoffeeCardSkeleton, CoffeesStore } from '@coffee-tracker/coffees';

@Component({
  selector: 'app-home',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Button, CountUp, Icon, BeanScene, CoffeeCard, CoffeeCardSkeleton],
  templateUrl: './home.html',
})
export class Home {
  protected readonly store = inject(CoffeesStore);

  /**
   * The catalog list. `CoffeesStore.coffees` already returns [] while the resource is
   * in its error state (the store guards the throwing httpResource value), so reads
   * here are safe; the template shows a retry block when `store.error()` is set.
   */
  protected readonly coffees = this.store.coffees;

  /**
   * The most recent few bags for the "Fresh on the shelf" teaser. Sort explicitly by
   * id (newest first) so the teaser is self-contained and doesn't silently break if
   * the catalog API's default ordering ever changes.
   */
  protected readonly recent = computed(() =>
    [...this.coffees()].sort((a, b) => b.id - a.id).slice(0, 4),
  );
  protected readonly skeletons = Array.from({ length: 4 });

  /** Headline numbers for the hero stat strip. */
  protected readonly stats = computed(() => {
    const list = this.coffees();
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

import { ChangeDetectionStrategy, Component } from '@angular/core';
import { Skeleton } from '@coffee-tracker/ui';

/**
 * Loading placeholder that mirrors {@link CoffeeCard}'s shape. Shared by the grid
 * and the home teaser so the two stay in sync. Decorative — callers mark the
 * surrounding grid `aria-busy` and announce loading via an sr-only status.
 */
@Component({
  selector: 'ct-coffee-card-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Skeleton],
  template: `
    <div class="overflow-hidden rounded-2xl border border-line bg-foam" aria-hidden="true">
      <ct-skeleton height="7rem" width="100%" />
      <div class="flex flex-col gap-2 p-3">
        <ct-skeleton height="1.1rem" width="80%" />
        <ct-skeleton height="0.8rem" width="55%" />
      </div>
    </div>
  `,
})
export class CoffeeCardSkeleton {}

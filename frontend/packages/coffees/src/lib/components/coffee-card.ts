import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Card, Rating, TagChip } from '@coffee-tracker/ui';
import { formatRating } from '@coffee-tracker/util';
import type { Coffee } from '@coffee-tracker/data';
import { roastBucket, roastGradient } from '../utils/coffee-visual';

/**
 * One coffee as a shelf card — the roast-gradient (or photo) label, name, roaster and
 * average rating. Shared by the Browse grid and the Home "Fresh on the shelf" section so
 * they render identically. `index` drives the staggered `ct-rise` entrance.
 */
@Component({
  selector: 'ct-coffee-card',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Card, Rating, TagChip],
  template: `
    <a [routerLink]="['/coffees', coffee().id]" class="ct-rise" [style.animationDelay]="(index() % 12) * 40 + 'ms'">
      <ct-card [interactive]="true">
        <div class="relative h-32 text-foam" [style.background]="roastGradient(coffee().roastLevel)">
          @if (coffee().photoPath) {
            <img
              [src]="'/' + coffee().photoPath"
              [alt]="coffee().name"
              loading="lazy"
              decoding="async"
              class="absolute inset-0 size-full object-cover"
            />
          }
          <span class="absolute left-2 top-2 rounded-md bg-black/30 px-2 py-0.5 font-mono text-[10px] uppercase tracking-wide backdrop-blur">
            {{ coffee().origin }}
          </span>
          <span class="absolute right-2 top-2 rounded-md border border-white/25 bg-white/15 px-2 py-0.5 font-mono text-[10px] uppercase tracking-wide backdrop-blur">
            {{ roastBucket(coffee().roastLevel) }}
          </span>
        </div>
        <div class="p-3">
          <h3 class="truncate font-display text-lg font-semibold">{{ coffee().name }}</h3>
          <p class="truncate text-xs text-muted">{{ coffee().roaster }}</p>
          <div class="mt-2 flex items-center gap-2">
            <ct-rating [value]="coffee().averageRating ?? 0" size="sm" />
            <span class="font-mono text-xs font-semibold">{{ formatRating(coffee().averageRating) }}</span>
            <span class="text-xs text-muted">· {{ coffee().reviewCount }}</span>
          </div>
          @if (coffee().flavorTags.length) {
            <div class="mt-2.5 flex flex-wrap gap-1">
              @for (t of coffee().flavorTags.slice(0, 3); track t) {
                <ct-tag-chip>{{ t }}</ct-tag-chip>
              }
            </div>
          }
        </div>
      </ct-card>
    </a>
  `,
})
export class CoffeeCard {
  readonly coffee = input.required<Coffee>();
  readonly index = input(0);
  protected readonly roastGradient = roastGradient;
  protected readonly roastBucket = roastBucket;
  protected readonly formatRating = formatRating;
}

import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { Button } from '@coffee-tracker/ui';
import { CoffeeCardSkeleton } from './coffee-card-skeleton';

/**
 * The shared loading / error / empty / loaded scaffolding around a coffee-card grid.
 * Used by both the Browse grid and the Home "Fresh on the shelf" teaser so the three
 * states (and the grid layout itself) can't drift apart. The loaded cards are
 * projected into the grid via `<ng-content>`.
 */
@Component({
  selector: 'ct-coffee-shelf-states',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button, CoffeeCardSkeleton],
  template: `
    @if (loading()) {
      <div class="grid grid-cols-2 gap-3 sm:grid-cols-3 sm:gap-4 lg:grid-cols-4" aria-busy="true">
        <span class="sr-only" role="status">Loading coffees…</span>
        @for (s of skeletons(); track $index) {
          <ct-coffee-card-skeleton />
        }
      </div>
    } @else if (error(); as message) {
      <div class="rounded-2xl border border-line bg-foam p-8 text-center text-cocoa">
        {{ message }}
        <button (click)="retry.emit()" class="mt-3 block w-full font-semibold text-crema-deep">
          Try again
        </button>
      </div>
    } @else if (empty()) {
      <div class="rounded-2xl border border-dashed border-line bg-foam/60 p-10 text-center">
        <p class="font-display text-xl">Nothing on the shelf yet.</p>
        <p class="mt-1 text-sm text-muted">Add your first bag — or snap a photo of one.</p>
        <ct-button variant="crema" link="/coffees/new" class="mt-4 inline-flex">Add a coffee</ct-button>
      </div>
    } @else {
      <div class="grid grid-cols-2 gap-3 sm:grid-cols-3 sm:gap-4 lg:grid-cols-4">
        <ng-content />
      </div>
    }
  `,
})
export class CoffeeShelfStates {
  readonly loading = input.required<boolean>();
  readonly error = input<string | null>(null);
  readonly empty = input.required<boolean>();
  /** How many skeleton cards the loading grid shows. */
  readonly skeletonCount = input(4);
  readonly retry = output<void>();

  protected readonly skeletons = computed(() => Array.from({ length: this.skeletonCount() }));
}

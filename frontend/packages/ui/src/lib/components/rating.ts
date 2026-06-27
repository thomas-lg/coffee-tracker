import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';

/**
 * Coffee-gold star rating. Display mode shows a fractional fill (animated); set
 * `interactive` to let the user pick a whole-star value (emits `rated`).
 */
@Component({
  selector: 'ct-rating',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (interactive()) {
      <div class="inline-flex gap-1" role="group" aria-label="Rate this coffee">
        @for (n of stars; track n) {
          <button
            type="button"
            (click)="rated.emit(n)"
            (mouseenter)="hover.set(n)"
            (mouseleave)="hover.set(0)"
            [class]="sizeClass()"
            class="leading-none transition-transform hover:scale-110 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-crema"
            [class.text-crema]="n <= (hover() || value())"
            [class.text-line]="n > (hover() || value())"
            [attr.aria-label]="n + (n === 1 ? ' star' : ' stars')"
          >
            ★
          </button>
        }
      </div>
    } @else {
      <span class="relative inline-block leading-none tracking-[2px]" [class]="sizeClass()" [attr.aria-label]="label()">
        <span class="text-line">★★★★★</span>
        <span
          class="absolute inset-0 overflow-hidden whitespace-nowrap text-crema transition-[width] duration-700 ease-out"
          [style.width.%]="fillPct()"
          >★★★★★</span
        >
      </span>
    }
  `,
})
export class Rating {
  readonly value = input(0);
  readonly interactive = input(false);
  readonly size = input<'sm' | 'md' | 'lg'>('md');
  readonly rated = output<number>();

  protected readonly stars = [1, 2, 3, 4, 5];
  protected readonly hover = signal(0);
  protected readonly fillPct = computed(() => Math.max(0, Math.min(100, (this.value() / 5) * 100)));
  protected readonly label = computed(() => `${this.value().toFixed(1)} out of 5`);
  protected readonly sizeClass = computed(() => {
    const interactive = this.interactive();
    switch (this.size()) {
      case 'sm':
        return interactive ? 'text-base' : 'text-sm';
      case 'lg':
        return interactive ? 'text-3xl' : 'text-2xl';
      default:
        return interactive ? 'text-2xl' : 'text-base';
    }
  });
}

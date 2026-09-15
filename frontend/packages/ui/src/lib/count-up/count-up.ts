import { ChangeDetectionStrategy, Component, computed, effect, input, signal } from '@angular/core';
import { prefersReducedMotion } from '@coffee-tracker/util';

/**
 * Animates a number from 0 up to `value` with an eased rAF tween. Respects
 * `prefers-reduced-motion` (jumps straight to the final value) and re-runs whenever
 * `value` changes. Renders the formatted number as its text content.
 */
@Component({
  selector: 'ct-count-up',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `{{ display() }}`,
})
export class CountUp {
  readonly value = input.required<number>();
  readonly decimals = input(0);

  private readonly current = signal(0);
  protected readonly display = computed(() => this.current().toFixed(this.decimals()));

  constructor() {
    effect((onCleanup) => {
      const target = this.value();
      if (prefersReducedMotion() || typeof requestAnimationFrame === 'undefined') {
        this.current.set(target);
        return;
      }

      const duration = 900;
      let start = 0;
      let raf = 0;
      const tick = (now: number) => {
        if (!start) start = now;
        const p = Math.min(1, (now - start) / duration);
        const eased = 1 - Math.pow(1 - p, 3); // easeOutCubic
        this.current.set(target * eased);
        if (p < 1) raf = requestAnimationFrame(tick);
      };
      raf = requestAnimationFrame(tick);
      onCleanup(() => cancelAnimationFrame(raf));
    });
  }
}

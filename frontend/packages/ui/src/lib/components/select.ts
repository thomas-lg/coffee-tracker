import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { Icon } from './icon';

/**
 * Styled pill `<select>` with a chevron. Options are projected as `<option>` children
 * so callers keep full control of the list (static or `@for`); the wrapper owns the
 * shared styling so the filter dropdowns don't each re-declare it.
 */
@Component({
  selector: 'ct-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  template: `
    <div class="relative inline-flex">
      <select
        #el
        [value]="value()"
        (change)="valueChange.emit(el.value)"
        [attr.aria-label]="ariaLabel()"
        class="h-11 appearance-none rounded-full border border-line bg-foam pl-4 pr-10 text-sm font-semibold"
      >
        <ng-content />
      </select>
      <span class="pointer-events-none absolute inset-y-0 right-3 flex items-center text-muted">
        <ct-icon name="chevron-down" [size]="14" />
      </span>
    </div>
  `,
})
export class Select {
  readonly value = input('');
  readonly ariaLabel = input('');
  readonly valueChange = output<string>();
}

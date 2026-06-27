import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Primary action button in the coffee theme. Variant maps to a token combo;
 * everything is signal inputs (zoneless-friendly).
 */
@Component({
  selector: 'ct-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      [type]="type()"
      [disabled]="disabled()"
      [class]="classes[variant()]"
      class="inline-flex items-center justify-center gap-2 rounded-full px-5 py-3 text-sm font-semibold transition-[transform,background-color,box-shadow] duration-150 active:scale-[0.98] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-crema disabled:pointer-events-none disabled:opacity-50"
    >
      <ng-content />
    </button>
  `,
})
export class Button {
  readonly type = input<'button' | 'submit'>('button');
  readonly variant = input<'primary' | 'crema' | 'ghost'>('primary');
  readonly disabled = input(false);

  protected readonly classes: Record<string, string> = {
    primary: 'bg-ink text-foam hover:opacity-90 shadow-sm',
    crema: 'bg-crema text-ink hover:bg-crema-deep hover:text-foam shadow-sm',
    ghost: 'bg-transparent text-ink ring-1 ring-line hover:ring-cocoa',
  };
}

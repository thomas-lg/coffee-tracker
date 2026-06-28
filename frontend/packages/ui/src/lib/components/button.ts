import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';

/**
 * Primary action button in the coffee theme. Variant maps to a token combo;
 * everything is signal inputs (zoneless-friendly). When `link` is set it renders a
 * real `<a routerLink>` (correct semantics + native pointer) styled identically to
 * the `<button>`; otherwise a `<button>`.
 *
 * The projected label lives in a single `<ng-content>` stamped into whichever wrapper
 * is active — two `<ng-content>` slots (one per branch) would drop the content.
 */
@Component({
  selector: 'ct-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NgTemplateOutlet],
  template: `
    <ng-template #label><ng-content /></ng-template>
    @if (link() != null) {
      <a [routerLink]="link()" [class]="cls()">
        <ng-container [ngTemplateOutlet]="label" />
      </a>
    } @else {
      <button [type]="type()" [disabled]="disabled()" [class]="cls()">
        <ng-container [ngTemplateOutlet]="label" />
      </button>
    }
  `,
})
export class Button {
  readonly type = input<'button' | 'submit'>('button');
  readonly variant = input<'primary' | 'crema' | 'ghost'>('primary');
  readonly disabled = input(false);
  /** When provided, renders an `<a routerLink>` instead of a `<button>`. */
  readonly link = input<string | readonly unknown[] | null>(null);

  private readonly base =
    'inline-flex cursor-pointer items-center justify-center gap-2 rounded-full px-5 py-3 text-sm font-semibold transition-[transform,background-color,box-shadow] duration-150 active:scale-[0.98] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-crema disabled:pointer-events-none disabled:opacity-50';

  private readonly classes: Record<string, string> = {
    primary: 'bg-ink text-foam hover:opacity-90 shadow-sm',
    crema: 'bg-crema text-ink hover:bg-crema-deep hover:text-foam shadow-sm',
    ghost: 'bg-transparent text-ink ring-1 ring-line hover:ring-cocoa',
  };

  protected readonly cls = computed(() => `${this.base} ${this.classes[this.variant()]}`);
}

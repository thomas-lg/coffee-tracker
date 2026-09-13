import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  booleanAttribute,
  computed,
  inject,
  input,
} from '@angular/core';
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
 *
 * `fullWidth` also blocks the host: the inner control is inline-flex, so stretching it
 * inside an inline host would leave the percentage resolving against the wrong box.
 */
@Component({
  selector: 'ct-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NgTemplateOutlet],
  host: { '[class.block]': 'fullWidth()' },
  template: `
    <ng-template #label><ng-content /></ng-template>
    @if (link() != null) {
      <a
        [routerLink]="disabled() ? null : link()"
        [class]="cls()"
        [class.pointer-events-none]="disabled()"
        [class.opacity-50]="disabled()"
        [attr.aria-disabled]="disabled() ? 'true' : null"
      >
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
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly type = input<'button' | 'submit'>('button');
  readonly variant = input<'primary' | 'crema' | 'ghost'>('primary');
  readonly disabled = input(false);
  /** When provided, renders an `<a routerLink>` instead of a `<button>`. */
  readonly link = input<string | readonly unknown[] | null>(null);
  /** Stretches the control across its column instead of hugging its label. */
  readonly fullWidth = input(false, { transform: booleanAttribute });

  private readonly base =
    'inline-flex cursor-pointer items-center justify-center gap-2 rounded-full px-5 py-3 text-sm font-semibold transition-[transform,background-color,box-shadow] duration-150 active:scale-[0.98] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-crema disabled:pointer-events-none disabled:opacity-50';

  private readonly classes: Record<'primary' | 'crema' | 'ghost', string> = {
    primary: 'bg-ink text-foam hover:opacity-90 shadow-sm',
    crema: 'bg-crema text-ink hover:bg-crema-deep hover:text-foam shadow-sm',
    ghost: 'bg-transparent text-ink ring-1 ring-line hover:ring-cocoa',
  };

  protected readonly cls = computed(
    () => `${this.base} ${this.classes[this.variant()]}${this.fullWidth() ? ' w-full' : ''}`,
  );

  /**
   * Moves focus to the real control. Callers hold a template reference to the component,
   * not to the element — the host itself is not focusable, and which element exists
   * depends on whether `link` is set. Used when a dismissed confirm has to put focus back
   * where it came from.
   */
  focus(): void {
    this.host.nativeElement.querySelector<HTMLElement>('button, a')?.focus();
  }
}

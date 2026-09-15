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
import { LucideCheck } from '@lucide/angular';
import { type ActionState, type ActionStateLike, toActionState } from '@coffee-tracker/util';

/** Variant names, shared with the components that wrap a button of their own. */
export type ButtonVariant = 'primary' | 'crema' | 'ghost' | 'danger';

/**
 * Primary action button in the coffee theme. Variant maps to a token combo;
 * everything is signal inputs (zoneless-friendly). When `link` is set it renders a
 * real `<a routerLink>` (correct semantics + native pointer) styled identically to
 * the `<button>`; otherwise a `<button>`.
 *
 * The projected label lives in a single `<ng-content>` stamped into whichever wrapper
 * is active, two `<ng-content>` slots (one per branch) would drop the content.
 *
 * `fullWidth` also blocks the host: the inner control is inline-flex, so stretching it
 * inside an inline host would leave the percentage resolving against the wrong box.
 *
 * `state` reports an action back, so a spinner, a tick, a label swap and an inert control
 * are one binding. It is not `disabled`: unavailable deserves the real attribute, but
 * momentarily busy does not, because the browser blurs an element that becomes disabled,
 * and that one is the element the user just pressed. Running is made inert with
 * `aria-disabled` and a swallowed click instead, which leaves focus where they put it.
 *
 * Swallowing the click blocks the submission it would have caused, but nothing here can
 * reach implicit submission from an input's Enter key, so a command behind a submit
 * refuses re-entry on its own (ours all use `exhaustMap`).
 */
@Component({
  selector: 'ct-button',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, NgTemplateOutlet, LucideCheck],
  host: {
    '[class.block]': 'fullWidth()',
    // For a caller that needs to style or assert on the state without this component
    // guessing what each one wants shown.
    '[attr.data-state]': "state() === 'idle' ? null : state()",
  },
  template: `
    <ng-template #content>
      @if (running()) {
        <!-- Drawn from the current text colour so it reads on every variant. -->
        <span
          class="size-3.5 shrink-0 animate-spin rounded-full border-2 border-current border-t-transparent motion-reduce:animate-none"
          aria-hidden="true"
        ></span>
      } @else if (done() && !disabled()) {
        <!-- Decorative: the outcome is already announced by the action's toast, and the
             state returns to rest on its own, so nothing here schedules the tick away. -->
        <svg lucideCheck [size]="16" class="shrink-0" aria-hidden="true"></svg>
      }
      @if (running() && runningLabel(); as label) {
        {{ label }}
      } @else {
        <ng-content />
      }
    </ng-template>
    @if (link() != null) {
      <a
        [routerLink]="inert() ? null : link()"
        [class]="cls()"
        [class.pointer-events-none]="inert()"
        [class.opacity-50]="disabled()"
        [attr.aria-disabled]="inert() ? 'true' : null"
        [attr.aria-busy]="running() ? 'true' : null"
      >
        <ng-container [ngTemplateOutlet]="content" />
      </a>
    } @else {
      <button
        [type]="type()"
        [disabled]="disabled()"
        [attr.aria-disabled]="running() ? 'true' : null"
        [attr.aria-busy]="running() ? 'true' : null"
        [class]="cls()"
        (click)="swallowWhileRunning($event)"
      >
        <ng-container [ngTemplateOutlet]="content" />
      </button>
    }
  `,
})
export class Button {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  readonly type = input<'button' | 'submit'>('button');
  readonly variant = input<ButtonVariant>('primary');
  readonly disabled = input(false);
  /** When provided, renders an `<a routerLink>` instead of a `<button>`. */
  readonly link = input<string | readonly unknown[] | null>(null);
  /** Stretches the control across its column instead of hugging its label. */
  readonly fullWidth = input(false, { transform: booleanAttribute });

  /** Whatever the caller's store already exposes: a boolean, or a request status. */
  readonly state = input<ActionState, ActionStateLike>('idle', { transform: toActionState });

  /** Replaces the projected label while running. Without one, the label stays put. */
  readonly runningLabel = input<string | null>(null);

  protected readonly running = computed(() => this.state() === 'running');
  protected readonly done = computed(() => this.state() === 'done');

  /** Nothing to act on, or already acting: either way a click must not land. */
  protected readonly inert = computed(() => this.disabled() || this.running());

  private readonly base =
    'inline-flex cursor-pointer items-center justify-center gap-2 rounded-full px-5 py-3 text-sm font-semibold transition-[transform,background-color,box-shadow] duration-150 active:scale-[0.98] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-crema disabled:pointer-events-none disabled:opacity-50';

  private readonly classes: Record<ButtonVariant, string> = {
    primary: 'bg-ink text-foam hover:opacity-90 shadow-sm',
    crema: 'bg-crema text-ink hover:bg-crema-deep hover:text-foam shadow-sm',
    ghost: 'bg-transparent text-ink ring-1 ring-line hover:ring-cocoa',
    // Destructive: outlined rather than solid, so the safe way out of a confirm row is
    // never the quieter-looking control.
    danger:
      'bg-transparent text-red-700 ring-1 ring-red-300 hover:bg-red-50 dark:text-red-400 dark:ring-red-900 dark:hover:bg-red-950',
  };

  protected readonly cls = computed(
    () =>
      `${this.base} ${this.classes[this.variant()]}` +
      `${this.fullWidth() ? ' w-full' : ''}${this.running() ? ' cursor-progress opacity-70' : ''}`,
  );

  /**
   * `aria-disabled` is a promise, not something the browser enforces. Stopped before the
   * event leaves the element, so a caller's `(click)` on `<ct-button>` never hears it.
   */
  protected swallowWhileRunning(event: Event): void {
    if (!this.running()) return;
    event.preventDefault();
    event.stopPropagation();
  }

  /**
   * Moves focus to the real control. Callers hold a template reference to the component,
   * not to the element, the host itself is not focusable, and which element exists
   * depends on whether `link` is set. Used when a dismissed confirm has to put focus back
   * where it came from.
   */
  focus(): void {
    this.host.nativeElement.querySelector<HTMLElement>('button, a')?.focus();
  }
}

import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import {
  type ActionState,
  type ActionStateLike,
  onActionSettled,
  toActionState,
} from '@coffee-tracker/util';
import { Button, type ButtonVariant } from '../button/button';

/**
 * Two-step destructive action: the visible control arms a confirm row in place, rather
 * than opening a modal.
 *
 * Which of the two branches is showing is local state and not the caller's: it is nothing
 * but what is on screen, and a store could not act on it anyway, since focus lives in the
 * DOM. Callers own the outcome only — `confirmed`, and `state` coming back.
 *
 * Projected content sits in the armed branch, so the neighbours the action belongs with
 * (Edit, a selection count) are gone while a confirmation is pending.
 */
@Component({
  selector: 'ct-confirm-action',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button],
  template: `
    <!-- aria-live on the wrapper, which is the only ancestor that outlives both branches
         and can therefore announce the swap. -->
    <div
      class="flex flex-wrap items-center gap-2"
      aria-live="polite"
      [attr.aria-busy]="busy() || null"
    >
      @if (!confirming()) {
        <ng-content />
        <ct-button #armBtn [variant]="armVariant()" [disabled]="disabled()" (click)="arm()">
          {{ label() }}
        </ct-button>
      } @else {
        <span class="text-sm font-semibold text-ink">{{ prompt() }}</span>
        <button
          #cancelBtn
          type="button"
          [disabled]="busy()"
          (click)="cancel()"
          class="rounded-full px-3 py-3 text-sm font-semibold text-muted hover:text-ink disabled:opacity-60"
        >
          {{ cancelLabel() }}
        </button>
        <ct-button
          [variant]="confirmVariant()"
          [state]="state()"
          [runningLabel]="busyLabel()"
          (click)="confirm()"
        >
          {{ confirmLabel() }}
        </ct-button>
      }
    </div>
  `,
})
export class ConfirmAction {
  readonly label = input.required<string>();

  /** What the confirm row asks. Say what will be lost, not just "are you sure". */
  readonly prompt = input.required<string>();

  readonly confirmLabel = input('Confirm delete');
  readonly cancelLabel = input('Cancel');
  readonly busyLabel = input('Working…');

  readonly armVariant = input<ButtonVariant>('ghost');
  readonly confirmVariant = input<ButtonVariant>('danger');

  /** Blocks arming, for an action with nothing to act on yet. */
  readonly disabled = input(false);

  /** Whatever the caller's store already exposes: a boolean, or a request status. */
  readonly state = input<ActionState, ActionStateLike>('idle', { transform: toActionState });

  readonly confirmed = output<void>();

  private readonly injector = inject(Injector);
  private readonly armBtn = viewChild<Button>('armBtn');
  private readonly cancelBtn = viewChild<ElementRef<HTMLButtonElement>>('cancelBtn');

  protected readonly confirming = signal(false);

  protected readonly busy = computed(() => this.state() === 'running');

  constructor() {
    // A screen that stays mounted after the action (a refused delete) would otherwise
    // keep a confirm row that is no longer about anything.
    onActionSettled(this.state, () => {
      if (untracked(this.confirming)) this.setConfirming(false);
    });
  }

  protected arm(): void {
    if (this.disabled()) return;
    this.setConfirming(true);
  }

  protected cancel(): void {
    if (this.busy()) return;
    this.setConfirming(false);
  }

  protected confirm(): void {
    if (this.busy()) return;
    this.confirmed.emit();
  }

  /**
   * Every transition destroys the element that had focus, and a removed element drops it
   * to <body>, leaving a keyboard user at the top of the document mid-flow. Driven from
   * the state rather than the click, so the transitions that are not clicks are covered
   * too. (`autofocus` would say it declaratively; the template a11y lint bans it, and it
   * would also fire on the first render, which must not steal focus at all.)
   *
   * afterNextRender because the element to focus belongs to the branch this write is
   * about to create. An effect is no better: it runs before the view it is waiting for.
   */
  private setConfirming(next: boolean): void {
    this.confirming.set(next);
    afterNextRender(
      () => (next ? this.cancelBtn()?.nativeElement.focus() : this.armBtn()?.focus()),
      { injector: this.injector },
    );
  }
}

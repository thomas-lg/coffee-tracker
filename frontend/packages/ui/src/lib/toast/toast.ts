import {
  ChangeDetectionStrategy,
  Component,
  Injectable,
  computed,
  inject,
  signal,
} from '@angular/core';

export type ToastTone = 'success' | 'info' | 'error';
export interface ToastMessage {
  id: number;
  text: string;
  tone: ToastTone;
}

/** Signal-backed toast queue. Inject anywhere and call `show(...)`. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 0;
  readonly toasts = signal<readonly ToastMessage[]>([]);

  show(text: string, tone: ToastTone = 'success', durationMs = 2800): void {
    const id = ++this.nextId;
    this.toasts.update((list) => [...list, { id, text, tone }]);
    setTimeout(() => this.dismiss(id), durationMs);
  }

  dismiss(id: number): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }
}

/**
 * Renders the toast queue. Drop one `<ct-toast />` in the app shell.
 *
 * The two live regions are always in the DOM, empty most of the time, and that is the
 * whole point: a screen reader announces text that appears *inside* a region it is
 * already watching. `role` used to sit on the toast element itself, which @for creates
 * at the moment there is something to say, so the region and its content arrived
 * together and the announcement was missed as often as not.
 *
 * Two regions rather than one, because urgency is a property of the region: errors
 * interrupt (assertive), everything else waits for a pause (polite). A single region
 * cannot be both.
 */
@Component({
  selector: 'ct-toast',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pointer-events-none fixed inset-x-0 bottom-5 z-80 flex flex-col items-center gap-2 px-4"
    >
      <!-- Empty regions collapse to zero height; the flex gap around one costs 8px of
           dead space, which is cheaper than the announcement this buys. -->
      <div class="flex flex-col items-center gap-2" role="status" aria-live="polite">
        @for (t of polite(); track t.id) {
          <div [class]="pill">
            <span
              class="size-2 rounded-full"
              [class.bg-moss]="t.tone === 'success'"
              [class.bg-crema]="t.tone === 'info'"
            ></span>
            {{ t.text }}
          </div>
        }
      </div>
      <div class="flex flex-col items-center gap-2" role="alert" aria-live="assertive">
        @for (t of errors(); track t.id) {
          <div [class]="pill">
            <span class="size-2 rounded-full bg-red-400"></span>
            {{ t.text }}
          </div>
        }
      </div>
    </div>
  `,
})
export class Toast {
  private readonly toasts = inject(ToastService);

  protected readonly polite = computed(() =>
    this.toasts.toasts().filter((t) => t.tone !== 'error'),
  );
  protected readonly errors = computed(() =>
    this.toasts.toasts().filter((t) => t.tone === 'error'),
  );

  protected readonly pill =
    'pointer-events-auto flex items-center gap-2 rounded-full bg-ink px-4 py-2.5 ' +
    'text-sm font-semibold text-foam shadow-lg';
}

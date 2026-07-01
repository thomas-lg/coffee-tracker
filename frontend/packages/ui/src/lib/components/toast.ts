import { ChangeDetectionStrategy, Component, Injectable, inject, signal } from '@angular/core';

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

/** Renders the toast queue. Drop one `<ct-toast />` in the app shell. */
@Component({
  selector: 'ct-toast',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="pointer-events-none fixed inset-x-0 bottom-5 z-80 flex flex-col items-center gap-2 px-4">
      @for (t of toasts.toasts(); track t.id) {
        <div
          class="pointer-events-auto flex items-center gap-2 rounded-full bg-ink px-4 py-2.5 text-sm font-semibold text-foam shadow-lg"
          [attr.role]="t.tone === 'error' ? 'alert' : 'status'"
        >
          <span
            class="size-2 rounded-full"
            [class.bg-moss]="t.tone === 'success'"
            [class.bg-crema]="t.tone === 'info'"
            [class.bg-red-400]="t.tone === 'error'"
          ></span>
          {{ t.text }}
        </div>
      }
    </div>
  `,
})
export class Toast {
  protected readonly toasts = inject(ToastService);
}

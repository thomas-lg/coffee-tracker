import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Button, ToastService } from '@coffee-tracker/ui';
import { PhotoCleanupStore } from '../services/photo-cleanup.store';

/**
 * Admin photo-cleanup screen: audit every stored photo (used vs orphaned) and delete
 * a selected set of orphans. Used photos aren't selectable — the API skips them, and
 * disabling selection makes that guarantee visible.
 */
@Component({
  selector: 'ct-photo-cleanup',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button],
  providers: [PhotoCleanupStore],
  templateUrl: './photo-cleanup.html',
})
export class PhotoCleanup {
  protected readonly store = inject(PhotoCleanupStore);
  private readonly toast = inject(ToastService);

  /** Two-step delete: the action button arms a confirm row rather than a modal. */
  protected readonly confirming = signal(false);
  /** In-flight state belongs to the command, which the store now owns. */
  protected readonly deleting = this.store.pending;
  private readonly cancelBtn = viewChild<ElementRef<HTMLButtonElement>>('cancelBtn');

  constructor() {
    // Arming the confirm removes the Delete button (focus would drop to <body>);
    // move it to Cancel once the confirm controls exist in the DOM.
    effect(() => {
      if (this.confirming()) this.cancelBtn()?.nativeElement.focus();
    });

    // The delete is fire-and-forget now, so its outcome arrives as state. Acknowledging
    // it clears the slice this effect reads, which is what makes the toast fire once
    // rather than on every later render.
    effect(() => {
      const outcome = this.store.lastOutcome();
      if (!outcome) return;

      if (outcome.kind === 'ok') {
        this.toast.show(
          `Deleted ${outcome.result.deleted}, skipped ${outcome.result.skipped}`,
          'success',
        );
      } else {
        this.toast.show(this.store.requestError() ?? 'Delete failed — please retry.', 'error');
      }

      this.confirming.set(false);
      this.store.acknowledgeOutcome();
    });
  }

  /** Display name = the filename without the `photos/` prefix. */
  protected fileName(path: string): string {
    return path.slice(path.lastIndexOf('/') + 1);
  }

  protected arm(): void {
    if (this.store.selectedCount() > 0) {
      this.confirming.set(true);
    }
  }

  protected cancel(): void {
    this.confirming.set(false);
  }

  protected confirmDelete(): void {
    this.store.deleteSelected();
  }
}

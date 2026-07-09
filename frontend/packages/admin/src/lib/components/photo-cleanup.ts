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
  protected readonly deleting = signal(false);
  private readonly cancelBtn = viewChild<ElementRef<HTMLButtonElement>>('cancelBtn');

  constructor() {
    // Arming the confirm removes the Delete button (focus would drop to <body>);
    // move it to Cancel once the confirm controls exist in the DOM.
    effect(() => {
      if (this.confirming()) this.cancelBtn()?.nativeElement.focus();
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

  protected async confirmDelete(): Promise<void> {
    this.deleting.set(true);
    try {
      const result = await this.store.deleteSelected();
      this.toast.show(`Deleted ${result.deleted}, skipped ${result.skipped}`, 'success');
    } catch {
      this.toast.show('Delete failed — please retry.', 'error');
    } finally {
      this.deleting.set(false);
      this.confirming.set(false);
    }
  }
}

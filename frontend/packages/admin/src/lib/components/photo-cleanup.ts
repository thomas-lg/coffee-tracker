import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { Button } from '@coffee-tracker/ui';
import { PhotoCleanupStore } from '../services/photo-cleanup.store';

/**
 * Admin photo-cleanup screen: audit every stored photo (used vs orphaned) and delete
 * a selected set of orphans. Used photos aren't selectable — the API skips them, and
 * disabling selection makes that guarantee visible.
 *
 * Everything stateful lives in the store and the template reads it directly. What is
 * left here is the one thing a store cannot own: focus, which needs the view.
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
  private readonly cancelBtn = viewChild<ElementRef<HTMLButtonElement>>('cancelBtn');

  constructor() {
    // Arming the confirm removes the Delete button (focus would drop to <body>);
    // move it to Cancel once the confirm controls exist in the DOM.
    effect(() => {
      if (this.store.confirming()) this.cancelBtn()?.nativeElement.focus();
    });
  }

  /** Display name = the filename without the `photos/` prefix. */
  protected fileName(path: string): string {
    return path.slice(path.lastIndexOf('/') + 1);
  }
}

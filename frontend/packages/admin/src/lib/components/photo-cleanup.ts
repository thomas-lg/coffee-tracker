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
    // Arming removes the Delete button, so focus would drop to <body>. autofocus on
    // the Cancel button does work here — @if inserts it for real, and all three engines
    // honour that — but the template a11y lint bans the attribute outright.
    effect(() => {
      if (this.store.confirming()) this.cancelBtn()?.nativeElement.focus();
    });
  }

  /** Display name = the filename without the `photos/` prefix. */
  protected fileName(path: string): string {
    return path.slice(path.lastIndexOf('/') + 1);
  }
}

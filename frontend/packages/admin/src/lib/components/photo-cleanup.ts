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
 * a selected set of orphans. Used photos aren't selectable, the API skips them, and
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
  private readonly armBtn = viewChild<Button>('armBtn');

  constructor() {
    // Arming removes the Delete button and cancelling removes the Cancel button, so
    // either way the focused element disappears and focus drops to <body>. autofocus on
    // the Cancel button does work here, @if inserts it for real, and all three engines
    // honour that, but the template a11y lint bans the attribute outright.
    //
    // Only on a transition: this effect also runs on first render, and focusing a
    // toolbar button merely because the screen loaded would yank focus from the reader.
    let wasConfirming: boolean | undefined;
    effect(() => {
      const confirming = this.store.confirming();
      if (wasConfirming !== undefined && wasConfirming !== confirming) {
        if (confirming) {
          this.cancelBtn()?.nativeElement.focus();
        } else {
          this.armBtn()?.focus();
        }
      }
      wasConfirming = confirming;
    });
  }

  /** Display name = the filename without the `photos/` prefix. */
  protected fileName(path: string): string {
    return path.slice(path.lastIndexOf('/') + 1);
  }
}

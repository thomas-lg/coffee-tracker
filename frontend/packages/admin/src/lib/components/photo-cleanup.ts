import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
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
  private readonly injector = inject(Injector);
  private readonly cancelBtn = viewChild<ElementRef<HTMLButtonElement>>('cancelBtn');
  private readonly armBtn = viewChild<Button>('armBtn');

  /**
   * Arming and cancelling each destroy the button that had focus, so it would otherwise
   * drop to <body>. autofocus would do this too, and does work here because @if inserts
   * the button for real, but the template a11y lint bans the attribute outright.
   *
   * afterNextRender, not an effect on `confirming`: an effect runs before the template
   * that creates the button it wants to focus, so the viewChild is still empty when it
   * reads it and the focus silently never happens. Waiting for the render is the whole
   * point, and it is what the backup screen's confirm step already does.
   */
  protected arm(): void {
    this.store.arm();
    this.focusAfterRender(() => this.cancelBtn()?.nativeElement.focus());
  }

  protected cancel(): void {
    this.store.cancel();
    this.focusAfterRender(() => this.armBtn()?.focus());
  }

  private focusAfterRender(focus: () => void): void {
    afterNextRender(focus, { injector: this.injector });
  }

  /** Display name = the filename without the `photos/` prefix. */
  protected fileName(path: string): string {
    return path.slice(path.lastIndexOf('/') + 1);
  }
}

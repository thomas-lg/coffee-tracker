import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ConfirmAction } from '@coffee-tracker/ui';
import { PhotoCleanupStore } from './photo-cleanup.store';

/**
 * Admin photo-cleanup screen: audit every stored photo (used vs orphaned) and delete
 * a selected set of orphans. Used photos aren't selectable, the API skips them, and
 * disabling selection makes that guarantee visible.
 */
@Component({
  selector: 'ct-photo-cleanup',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ConfirmAction],
  providers: [PhotoCleanupStore],
  templateUrl: './photo-cleanup.html',
})
export class PhotoCleanup {
  protected readonly store = inject(PhotoCleanupStore);

  /** Display name = the filename without the `photos/` prefix. */
  protected fileName(path: string): string {
    return path.slice(path.lastIndexOf('/') + 1);
  }
}

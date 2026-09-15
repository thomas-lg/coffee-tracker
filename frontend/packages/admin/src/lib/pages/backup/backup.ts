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
import { BackupStore } from './backup.store';

/**
 * Admin backup screen: download the whole catalog as JSON, or restore one.
 *
 * A restore replaces the catalog, so choosing a file only arms a confirmation, the
 * same two-step shape the photo cleanup screen uses for its bulk delete.
 */
@Component({
  selector: 'ct-backup',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Button],
  providers: [BackupStore],
  templateUrl: './backup.html',
})
export class BackupScreen {
  protected readonly store = inject(BackupStore);
  private readonly injector = inject(Injector);
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');
  private readonly cancelBtn = viewChild<ElementRef<HTMLButtonElement>>('cancelBtn');

  protected browse(): void {
    this.fileInput()?.nativeElement.click();
  }

  protected async pick(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    // Cleared either way: picking the same file twice must still fire `change`, and a
    // refused file should not stay named in the control as if it had been accepted.
    input.value = '';
    if (!file) return;

    await this.store.choose(file);
    if (!this.store.staged()) return; // refused, the toast said why, focus stays put

    // Staging replaces the picker with the confirm row, so the button that had focus is
    // gone and focus drops to <body>. The row only exists after the next render, which
    // is what this waits for.
    afterNextRender(() => this.cancelBtn()?.nativeElement.focus(), { injector: this.injector });
  }
}

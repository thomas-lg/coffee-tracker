import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { AdminSettingsApi, type AccountSettings } from '@coffee-tracker/data';
import { ToastService } from '@coffee-tracker/ui';

@Component({
  selector: 'ct-account-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './account-settings.html',
})
export class AccountSettingsScreen {
  private readonly api = inject(AdminSettingsApi);
  private readonly toast = inject(ToastService);

  private readonly settingsRes = rxResource({ stream: () => this.api.get() });

  /**
   * Reading value() on an errored resource throws, and this one is read straight from
   * the template, so a failed GET would throw during change detection and take the
   * screen down instead of reaching its own "could not load" branch.
   */
  protected readonly settings = computed(() =>
    this.settingsRes.error() ? undefined : this.settingsRes.value(),
  );
  protected readonly loading = this.settingsRes.isLoading;
  protected readonly saving = signal(false);

  /**
   * The API's explanation when it refuses to disable local sign-in. Kept on screen
   * rather than flashed as a toast: it tells the admin what to do next, and they will
   * be reading it while deciding.
   */
  protected readonly refusal = signal<string | null>(null);

  protected async toggleLocalLogin(box: HTMLInputElement): Promise<void> {
    if (this.putBack(box)) return;
    await this.save({ ...this.current(), localLoginEnabled: box.checked });
  }

  protected async toggleRegistration(box: HTMLInputElement): Promise<void> {
    if (this.putBack(box)) return;
    await this.save({ ...this.current(), localRegistrationEnabled: box.checked });
  }

  /**
   * A click arriving mid-save is refused, and the box has already moved: `[checked]` is
   * only written back when its bound value changes, and a save in flight has not changed
   * anything yet, so nothing else would put it right.
   */
  private putBack(box: HTMLInputElement): boolean {
    if (!this.saving()) return false;
    box.checked = !box.checked;
    return true;
  }

  private current(): AccountSettings {
    return this.settings() ?? { localLoginEnabled: true, localRegistrationEnabled: false };
  }

  private async save(next: AccountSettings): Promise<void> {
    if (this.saving()) return;
    this.saving.set(true);
    this.refusal.set(null);
    try {
      await firstValueFrom(this.api.update(next));
      this.settingsRes.reload();
      this.toast.show('Account settings saved.', 'success');
    } catch (err: unknown) {
      const status = (err as { status?: number })?.status;
      const detail = (err as { error?: { detail?: string } })?.error?.detail;
      if (status === 409 && detail) {
        // Not a failure to report and forget: the admin has to act on it.
        this.refusal.set(detail);
      } else {
        this.toast.show('Could not save the account settings.', 'error');
      }
      // Either way the stored value is the truth; re-read rather than trust the toggle.
      this.settingsRes.reload();
    } finally {
      this.saving.set(false);
    }
  }
}

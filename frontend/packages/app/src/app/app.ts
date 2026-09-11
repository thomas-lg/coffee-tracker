import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore, ProviderSignIn } from '@coffee-tracker/auth';
import { Icon, Toast } from '@coffee-tracker/ui';
import { applyTheme, initialTheme, persistTheme, type ThemeMode } from '@coffee-tracker/util';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Toast, Icon],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  /** Restored from the user's persisted choice, falling back to the OS preference. */
  protected readonly theme = signal<ThemeMode>(initialTheme());

  /** 1–2 letter initials for the header avatar. */
  protected readonly initials = computed(() => {
    const name = this.auth.displayName()?.trim();
    if (!name) return '?';
    const parts = name.split(/\s+/);
    return ((parts[0]?.[0] ?? '?') + (parts[1]?.[0] ?? '')).toUpperCase();
  });

  private readonly provider = inject(ProviderSignIn);

  constructor() {
    applyTheme(this.theme());

    // The provider redirects back to the app root, so the callback has to be picked up
    // here rather than on a dedicated route. On an ordinary page load there is nothing
    // to complete and this is a no-op.
    void this.completeProviderSignIn();
  }

  private async completeProviderSignIn(): Promise<void> {
    if (await this.provider.complete()) {
      await this.router.navigateByUrl('/');
    } else if (this.provider.error()) {
      await this.router.navigateByUrl('/login');
    }
  }

  protected toggleTheme(): void {
    this.theme.update((m) => (m === 'dark' ? 'light' : 'dark'));
    applyTheme(this.theme());
    persistTheme(this.theme());
  }

  protected logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}

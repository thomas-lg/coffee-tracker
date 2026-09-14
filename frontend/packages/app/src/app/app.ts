import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore, ProviderSignIn } from '@coffee-tracker/auth';
import { Toast } from '@coffee-tracker/ui';
import { LucideMoon, LucideSun } from '@lucide/angular';
import { applyTheme, initialTheme, persistTheme, type ThemeMode } from '@coffee-tracker/util';

@Component({
  selector: 'ct-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Toast, LucideSun, LucideMoon],
  templateUrl: './app.html',
})
export class App {
  protected readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  /** Restored from the user's persisted choice, falling back to the OS preference. */
  protected readonly theme = signal<ThemeMode>(initialTheme());

  /** 1-2 letter initials for the header avatar. */
  protected readonly initials = computed(() => {
    const name = this.auth.displayName()?.trim();
    if (!name) return '?';
    const parts = name.split(/\s+/);
    return ((parts[0]?.[0] ?? '?') + (parts[1]?.[0] ?? '')).toUpperCase();
  });

  constructor() {
    applyTheme(this.theme());

    // Land on the app after a provider sign-in. No race with the router this time:
    // the exchange happens in an app initializer, so the flag is already settled here.
    if (inject(ProviderSignIn).justSignedIn()) {
      void this.router.navigateByUrl('/');
    }
  }

  protected toggleTheme(): void {
    this.theme.update((m) => (m === 'dark' ? 'light' : 'dark'));
    applyTheme(this.theme());
    persistTheme(this.theme());
  }
}

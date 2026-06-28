import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '@coffee-tracker/auth';
import { Icon, Toast } from '@coffee-tracker/ui';
import { applyTheme, prefersDark, type ThemeMode } from '@coffee-tracker/util';

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
  protected readonly theme = signal<ThemeMode>(prefersDark() ? 'dark' : 'light');

  /** 1–2 letter initials for the header avatar. */
  protected readonly initials = computed(() => {
    const name = this.auth.displayName()?.trim();
    if (!name) return '?';
    const parts = name.split(/\s+/);
    return (parts[0][0] + (parts[1]?.[0] ?? '')).toUpperCase();
  });

  constructor() {
    applyTheme(this.theme());
  }

  protected toggleTheme(): void {
    this.theme.update((m) => (m === 'dark' ? 'light' : 'dark'));
    applyTheme(this.theme());
  }

  protected logout(): void {
    this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}

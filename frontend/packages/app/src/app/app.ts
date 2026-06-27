import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '@coffee-tracker/auth';
import { Toast } from '@coffee-tracker/ui';
import { applyTheme, prefersDark, type ThemeMode } from '@coffee-tracker/util';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Toast],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  protected readonly theme = signal<ThemeMode>(prefersDark() ? 'dark' : 'light');

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

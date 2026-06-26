import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Button } from '@coffee-tracker/ui';
import { applyTheme, prefersDark, type ThemeMode } from '@coffee-tracker/util';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, Button],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly theme = signal<ThemeMode>(prefersDark() ? 'dark' : 'light');

  constructor() {
    applyTheme(this.theme());
  }

  protected toggleTheme(): void {
    this.theme.update((m) => (m === 'dark' ? 'light' : 'dark'));
    applyTheme(this.theme());
  }
}

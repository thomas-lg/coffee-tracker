import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'ct-admin-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <section class="mx-auto max-w-5xl px-5 py-8">
      <header class="mb-8">
        <p class="font-mono text-xs uppercase tracking-widest text-crema-deep">Admin</p>
        <nav class="mt-3 flex gap-1 border-b border-line" aria-label="Admin sections">
          <a
            routerLink="photos"
            routerLinkActive
            #photos="routerLinkActive"
            [attr.aria-current]="photos.isActive ? 'page' : null"
            class="-mb-px border-b-2 px-4 pb-3 text-sm font-semibold transition-colors"
            [class]="photos.isActive ? active : idle"
            >Photos</a
          >
          <a
            routerLink="settings"
            routerLinkActive
            #settings="routerLinkActive"
            [attr.aria-current]="settings.isActive ? 'page' : null"
            class="-mb-px border-b-2 px-4 pb-3 text-sm font-semibold transition-colors"
            [class]="settings.isActive ? active : idle"
            >Accounts</a
          >
          <a
            routerLink="backup"
            routerLinkActive
            #backup="routerLinkActive"
            [attr.aria-current]="backup.isActive ? 'page' : null"
            class="-mb-px border-b-2 px-4 pb-3 text-sm font-semibold transition-colors"
            [class]="backup.isActive ? active : idle"
            >Backup</a
          >
        </nav>
      </header>

      <router-outlet />
    </section>
  `,
})
export class AdminShell {
  // Not `routerLinkActive="..."`: the active and idle states differ by border-colour, and
  // two competing `border-*` utilities in the same Tailwind layer are settled by generation
  // order, not by the directive. Binding one whole class string leaves nothing to chance.
  protected readonly active = 'border-crema text-ink';
  protected readonly idle = 'border-transparent text-muted hover:border-line hover:text-ink';
}

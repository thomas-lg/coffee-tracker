import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'ct-admin-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <section class="mx-auto max-w-5xl px-5 py-8">
      <header class="mb-6">
        <p class="font-mono text-xs uppercase tracking-widest text-crema-deep">Admin</p>
        <nav class="mt-3 flex gap-1" aria-label="Admin sections">
          <a
            routerLink="photos"
            routerLinkActive="bg-ink text-porcelain"
            class="rounded-full px-4 py-1.5 text-sm font-semibold text-muted hover:text-ink"
            >Photos</a
          >
          <a
            routerLink="settings"
            routerLinkActive="bg-ink text-porcelain"
            class="rounded-full px-4 py-1.5 text-sm font-semibold text-muted hover:text-ink"
            >Accounts</a
          >
        </nav>
      </header>

      <router-outlet />
    </section>
  `,
})
export class AdminShell {}

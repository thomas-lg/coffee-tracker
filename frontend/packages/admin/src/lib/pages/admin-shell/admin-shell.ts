import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

/** A tab in the admin bar. */
interface AdminSection {
  path: string;
  label: string;
  /** `settings` changes how the instance behaves; `maintenance` tidies up after it. */
  group: 'settings' | 'maintenance';
}

/** A tab plus whether the group changes at it, worked out once rather than in the template. */
interface AdminTab extends AdminSection {
  opensGroup: boolean;
}

@Component({
  selector: 'ct-admin-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <section class="mx-auto max-w-5xl px-5 py-8">
      <header class="mb-8">
        <p class="font-mono text-xs uppercase tracking-widest text-crema-deep">Admin</p>
        <nav
          class="mt-3 flex flex-wrap items-center gap-1 border-b border-line"
          aria-label="Admin sections"
        >
          @for (section of tabs; track section.path) {
            <!-- A hairline where the group changes: the first two decide how the
                 instance behaves, the last two clean up after it. Decoration only, so
                 it is hidden from assistive tech rather than read as a separator
                 between two lists that are really one. -->
            @if (section.opensGroup) {
              <span class="mx-2 h-4 w-px self-center bg-line" aria-hidden="true"></span>
            }
            <a
              [routerLink]="section.path"
              routerLinkActive
              #link="routerLinkActive"
              [attr.aria-current]="link.isActive ? 'page' : null"
              class="-mb-px border-b-2 px-4 pb-3 text-sm font-semibold transition-colors"
              [class]="link.isActive ? active : idle"
              >{{ section.label }}</a
            >
          }
        </nav>
      </header>

      <router-outlet />
    </section>
  `,
})
export class AdminShell {
  /**
   * The tab order, and the only place it is declared. Settings first, because they are
   * what an administrator comes here to change; maintenance after, because it is what
   * they come here to run. `/admin` lands on the first of them.
   */
  static readonly Sections: readonly AdminSection[] = [
    { path: 'accounts', label: 'Accounts', group: 'settings' },
    { path: 'scanning', label: 'Scanning', group: 'settings' },
    { path: 'photos', label: 'Photos', group: 'maintenance' },
    { path: 'backup', label: 'Backup', group: 'maintenance' },
  ];

  protected readonly tabs: readonly AdminTab[] = AdminShell.Sections.map((section, i) => ({
    ...section,
    opensGroup: i > 0 && section.group !== AdminShell.Sections[i - 1]?.group,
  }));

  // Not `routerLinkActive="..."`: the active and idle states differ by border-colour, and
  // two competing `border-*` utilities in the same Tailwind layer are settled by generation
  // order, not by the directive. Binding one whole class string leaves nothing to chance.
  protected readonly active = 'border-crema text-ink';
  protected readonly idle = 'border-transparent text-muted hover:border-line hover:text-ink';
}

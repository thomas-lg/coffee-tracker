import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ADMIN_ROUTES } from '@admin/admin.routes';
import { AdminShell } from './admin-shell';

// The shell had no spec, so no test build ever compiled its template and a type error in
// it only surfaced when someone started the dev server. That alone earns this file.
describe('AdminShell', () => {
  let fixture: ReturnType<typeof TestBed.createComponent<AdminShell>>;

  const tabs = (): HTMLAnchorElement[] =>
    Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLAnchorElement>('nav a'),
    );

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [AdminShell],
      providers: [provideZonelessChangeDetection(), provideRouter([])],
    });
    fixture = TestBed.createComponent(AdminShell);
    fixture.detectChanges();
  });

  afterEach(() => TestBed.resetTestingModule());

  it('lists the sections in the declared order', () => {
    // The order is a deliberate grouping, settings before maintenance, not an accident
    // of when each tab was added.
    expect(tabs().map((a) => a.textContent?.trim())).toEqual([
      'Accounts',
      'Scanning',
      'Photos',
      'Backup',
    ]);
  });

  it('divides the two groups once, between them', () => {
    const dividers = (fixture.nativeElement as HTMLElement).querySelectorAll('nav [aria-hidden="true"]');

    expect(dividers).toHaveLength(1);
  });

  it('names the nav so a screen reader can jump to it', () => {
    const nav = (fixture.nativeElement as HTMLElement).querySelector('nav');

    expect(nav?.getAttribute('aria-label')).toMatch(/admin sections/i);
  });

  it('has a route for every tab, and lands on the first one', () => {
    // A tab whose route is missing renders a dead link, which the shell cannot notice
    // on its own: this is the check that keeps the two lists in step.
    const children = ADMIN_ROUTES[0]?.children ?? [];
    const paths = children.map((r) => r.path);

    for (const section of AdminShell.Sections) {
      expect(paths).toContain(section.path);
    }

    expect(children.find((r) => r.path === '')?.redirectTo).toBe(AdminShell.Sections[0]?.path);
  });

  it('still answers the URL Accounts used to live at', () => {
    // The paths were renamed to match their labels; a bookmark should not pay for that.
    const children = ADMIN_ROUTES[0]?.children ?? [];

    expect(children.find((r) => r.path === 'settings')?.redirectTo).toBe('accounts');
  });
});

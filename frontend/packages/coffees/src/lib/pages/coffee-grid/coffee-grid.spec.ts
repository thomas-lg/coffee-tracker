import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { CoffeeGrid } from './coffee-grid';

// CoffeesStore keys its resource on the signed-in user, so it stays idle until there is
// a session, seed one the way a real browser would before TestBed builds the store.
function seedSession(): void {
  localStorage.setItem(
    'ct.session',
    JSON.stringify({
      token: 't',
      userId: 'u1',
      displayName: 'Tester',
      isAdmin: false,
      expiresAt: new Date(Date.now() + 900_000).toISOString(),
    }),
  );
}

describe('CoffeeGrid', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    seedSession();
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  it('shows a retry block instead of crashing when the catalog fails to load', async () => {
    const fixture = TestBed.createComponent(CoffeeGrid);
    fixture.detectChanges(); // initial render + run the resource effect (issues the GET)
    http.expectOne('/api/coffees').flush('boom', { status: 500, statusText: 'Server Error' });

    await new Promise((resolve) => setTimeout(resolve, 0)); // let the resource settle into error

    // The header reads store.filtered() unconditionally; before the store guarded the
    // throwing resource value this rethrew during change detection and crashed the grid.
    expect(() => fixture.detectChanges()).not.toThrow();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Could not load your coffees.');
    expect(text).toContain('Try again');
  });
});

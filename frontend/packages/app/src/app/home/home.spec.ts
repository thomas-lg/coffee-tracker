import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { Home } from './home';

describe('Home', () => {
  let http: HttpTestingController;

  beforeEach(() => {
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
    const fixture = TestBed.createComponent(Home);
    fixture.detectChanges(); // initial render + run the httpResource effect (issues the GET)
    http.expectOne('/api/coffees').flush('boom', { status: 500, statusText: 'Server Error' });

    // The resource settles into its error state on a microtask/macrotask after flush.
    await new Promise((resolve) => setTimeout(resolve, 0));

    // Re-rendering with the resource in its error state must not throw — the guarded
    // coffees() accessor returns [] rather than letting httpResource.value rethrow.
    expect(() => fixture.detectChanges()).not.toThrow();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Could not load your coffees.');
    expect(text).toContain('Try again');
  });
});

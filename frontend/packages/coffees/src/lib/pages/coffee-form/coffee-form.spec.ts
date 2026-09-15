import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { ApplicationRef, provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import type { Coffee } from '@coffee-tracker/data';
import { CoffeesStore } from '@coffees/services/coffees.store';
import { CoffeeForm } from './coffee-form';

function coffee(p: Partial<Coffee> & Pick<Coffee, 'id' | 'name'>): Coffee {
  return {
    roaster: 'R',
    origin: 'Origin',
    roastLevel: 'Medium',
    price: 10,
    dateBought: '2026-06-01',
    photoUrl: null,
    shopName: null,
    purchaseUrl: null,
    createdAt: '2026-06-01T00:00:00Z',
    averageRating: null,
    reviewCount: 0,
    flavorTags: [],
    ...p,
  };
}

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

describe('CoffeeForm', () => {
  let http: HttpTestingController;
  let appRef: ApplicationRef;

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
    appRef = TestBed.inject(ApplicationRef);
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  function create() {
    // Seed the shared CoffeesStore first (resource GET → flush → tick), so the
    // shelf data is present before the form reads it.
    TestBed.inject(CoffeesStore);
    appRef.tick();
    http.expectOne('/api/coffees').flush([coffee({ id: 1, name: 'X', origin: 'Narnia' })]);
    appRef.tick();
    const fixture = TestBed.createComponent(CoffeeForm);
    appRef.tick();
    return fixture;
  }

  it('marks required fields invalid before anything is filled in', () => {
    const fixture = create();
    const form = fixture.nativeElement as HTMLElement;

    // Submitting an empty form is what a user does; the messages are the visible result.
    form.querySelector('form')?.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    const errors = [...form.querySelectorAll('.field-error')].map((e) => e.textContent?.trim());
    expect(errors).toContain('A name is required.');
    expect(errors).toContain('A roaster is required.');
    expect(errors).toContain('An origin is required.');
  });

  it('offers curated origin suggestions, sorted and deduped', () => {
    // (Shelf-merge of store.origins() is covered directly by CoffeesStore's spec.)
    const fixture = create();
    const options = [
      ...(fixture.nativeElement as HTMLElement).querySelectorAll('#originOptions option'),
    ].map((o) => o.getAttribute('value') ?? '');

    expect(options).toContain('Ethiopia');
    expect(options).toContain('Colombia');
    expect(options).toEqual([...options].sort());
    expect(new Set(options).size).toBe(options.length);
  });

  it('previews the photo already attached to the coffee being edited', async () => {
    const fixture = create();
    fixture.componentRef.setInput('id', '7');
    fixture.detectChanges();

    http
      .expectOne('/api/coffees/7')
      .flush(coffee({ id: 7, name: 'Edited', photoUrl: '/photos/a.jpg?exp=1&sig=x' }));
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();

    // Without this the edit screen shows no thumbnail at all, so nobody can tell which
    // photo is already attached.
    const img = (fixture.nativeElement as HTMLElement).querySelector('img[src*="/photos/a.jpg"]');
    expect(img).not.toBeNull();
  });
});

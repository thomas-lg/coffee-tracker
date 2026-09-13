import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { ApplicationRef, provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import type { Coffee } from '@coffee-tracker/data';
import { CoffeesStore } from '../services/coffees.store';
import { CoffeeForm } from './coffee-form';

function coffee(p: Partial<Coffee> & Pick<Coffee, 'id' | 'name'>): Coffee {
  return {
    roaster: 'R', origin: 'Origin', roastLevel: 'Medium', price: 10, dateBought: '2026-06-01',
    photoUrl: null, shopName: null, purchaseUrl: null, createdAt: '2026-06-01T00:00:00Z',
    averageRating: null, reviewCount: 0, flavorTags: [], ...p,
  };
}

describe('CoffeeForm', () => {
  let http: HttpTestingController;
  let appRef: ApplicationRef;

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

  it('is invalid before any required field is filled', () => {
    const fixture = create();
    const ci = fixture.componentInstance as unknown as { f: () => { invalid: () => boolean } };
    expect(ci.f().invalid()).toBe(true);
  });

  it('offers curated origin suggestions, sorted and deduped', () => {
    // (Shelf-merge of store.origins() is covered directly by CoffeesStore's spec.)
    const fixture = create();
    const sug = (fixture.componentInstance as unknown as { originSuggestions: () => string[] }).originSuggestions();
    expect(sug).toContain('Ethiopia');
    expect(sug).toContain('Colombia');
    expect(sug).toEqual([...sug].sort());
    expect(new Set(sug).size).toBe(sug.length);
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
    const img = fixture.nativeElement.querySelector('img[src*="/photos/a.jpg"]');
    expect(img).not.toBeNull();
  });
});

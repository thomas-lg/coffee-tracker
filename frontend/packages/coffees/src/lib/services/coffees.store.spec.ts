import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { ApplicationRef, provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import type { Coffee } from '@coffee-tracker/data';
import { CoffeesStore } from './coffees.store';

function coffee(p: Partial<Coffee> & Pick<Coffee, 'id' | 'name'>): Coffee {
  return {
    roaster: 'R',
    origin: 'Origin',
    roastLevel: 'Medium',
    price: 10,
    dateBought: '2026-06-01',
    photoPath: null,
    shopName: null,
    purchaseUrl: null,
    createdAt: '2026-06-01T00:00:00Z',
    averageRating: null,
    reviewCount: 0,
    flavorTags: [],
    ...p,
  };
}

// API order is newest-first; keep it that way in the seed.
const SEED: Coffee[] = [
  coffee({ id: 3, name: 'Geisha', roaster: 'Onyx', origin: 'Panama', roastLevel: 'Dark', averageRating: 4.5, reviewCount: 2, flavorTags: ['Citrus', 'Fruity'] }),
  coffee({ id: 2, name: 'Yirgacheffe', roaster: 'Tim Wendelboe', origin: 'Ethiopia', roastLevel: 'Light', averageRating: 4.8, reviewCount: 3, flavorTags: ['Floral'] }),
  coffee({ id: 1, name: 'Cerrado', roaster: 'Onyx', origin: 'Brazil', roastLevel: 'Medium', averageRating: 3.9, reviewCount: 1, shopName: 'Local Roast', flavorTags: ['Nutty', 'Chocolatey'] }),
];

describe('CoffeesStore', () => {
  let store: CoffeesStore;
  let http: HttpTestingController;
  let appRef: ApplicationRef;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        CoffeesStore,
      ],
    });
    store = TestBed.inject(CoffeesStore);
    http = TestBed.inject(HttpTestingController);
    appRef = TestBed.inject(ApplicationRef);

    // httpResource fetches from a reactive effect — tick to fire it, flush, tick again.
    appRef.tick();
    http.expectOne('/api/coffees').flush(SEED);
    appRef.tick();
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  it('loads the whole shelf by default (API order)', () => {
    expect(store.coffees().length).toBe(3);
    expect(store.filtered().map((c) => c.name)).toEqual(['Geisha', 'Yirgacheffe', 'Cerrado']);
  });

  it('filters by free-text search across name/roaster/origin', () => {
    store.search.set('yirga');
    expect(store.filtered().map((c) => c.name)).toEqual(['Yirgacheffe']);
    store.search.set('onyx');
    expect(store.filtered().map((c) => c.name).sort()).toEqual(['Cerrado', 'Geisha']);
  });

  it('filters by roast bucket', () => {
    store.roast.set('Light');
    expect(store.filtered().map((c) => c.name)).toEqual(['Yirgacheffe']);
  });

  it('filters by origin and by flavour', () => {
    store.origin.set('Brazil');
    expect(store.filtered().map((c) => c.name)).toEqual(['Cerrado']);
    store.origin.set('all');
    store.flavor.set('Floral');
    expect(store.filtered().map((c) => c.name)).toEqual(['Yirgacheffe']);
  });

  it('sorts by rating and by name', () => {
    store.setSort('rating');
    expect(store.filtered().map((c) => c.name)).toEqual(['Yirgacheffe', 'Geisha', 'Cerrado']);
    store.setSort('name');
    expect(store.filtered().map((c) => c.name)).toEqual(['Cerrado', 'Geisha', 'Yirgacheffe']);
  });

  it('derives deduped, sorted option lists', () => {
    expect(store.origins()).toEqual(['Brazil', 'Ethiopia', 'Panama']);
    expect(store.flavors()).toEqual(['Chocolatey', 'Citrus', 'Floral', 'Fruity', 'Nutty']);
    expect(store.roasters()).toEqual(['Onyx', 'Tim Wendelboe']);
    expect(store.shops()).toEqual(['Local Roast']);
  });
});

describe('CoffeesStore (error path)', () => {
  let store: CoffeesStore;
  let http: HttpTestingController;
  let appRef: ApplicationRef;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        CoffeesStore,
      ],
    });
    store = TestBed.inject(CoffeesStore);
    http = TestBed.inject(HttpTestingController);
    appRef = TestBed.inject(ApplicationRef);

    appRef.tick();
    http.expectOne('/api/coffees').flush('boom', { status: 500, statusText: 'Server Error' });
    appRef.tick();
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  it('surfaces a friendly error and stops loading', () => {
    // The grid guards the list behind @if(error()), so on error the store only needs
    // to expose the message and clear loading (reading .value would rethrow).
    expect(store.error()).toBe('Could not load your coffees.');
    expect(store.loading()).toBe(false);
  });
});

import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { ApplicationRef, provideZonelessChangeDetection, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import type { Coffee, Review } from '@coffee-tracker/data';
import { CoffeeDetailStore } from './coffee-detail.store';

const ME = 'me-1';
const SOMEONE_ELSE = 'them-2';

// CoffeesStore keys its resource on the signed-in user, and this store injects it —
// so there has to be a session before TestBed builds either.
function seedSession(): void {
  localStorage.setItem(
    'ct.session',
    JSON.stringify({
      token: 't',
      userId: ME,
      displayName: 'Tester',
      isAdmin: false,
      expiresAt: new Date(Date.now() + 900_000).toISOString(),
    }),
  );
}

function coffee(p: Partial<Coffee> = {}): Coffee {
  return {
    id: 7,
    name: 'Kirinyaga AA',
    roaster: 'La Cabra',
    origin: 'Kenya',
    roastLevel: 'Light',
    price: 18.5,
    dateBought: '2026-08-28',
    photoUrl: null,
    shopName: null,
    purchaseUrl: null,
    createdAt: '2026-08-28T00:00:00Z',
    averageRating: 4.5,
    reviewCount: 2,
    flavorTags: [],
    ...p,
  };
}

function review(p: Partial<Review> & Pick<Review, 'id' | 'userId'>): Review {
  return {
    coffeeId: 7,
    rating: 4,
    stage: null,
    tastingNotes: null,
    brewMethod: null,
    grind: null,
    ratio: null,
    createdAt: '2026-09-01T00:00:00Z',
    updatedAt: null,
    tags: [],
    ...p,
  };
}

describe('CoffeeDetailStore', () => {
  let http: HttpTestingController;
  let appRef: ApplicationRef;

  beforeEach(() => {
    seedSession();
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'coffees', children: [] }]),
        CoffeeDetailStore,
      ],
    });
    http = TestBed.inject(HttpTestingController);
    appRef = TestBed.inject(ApplicationRef);
  });

  afterEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  /**
   * rxResource publishes on a macrotask after the response, so settling it takes more
   * than a tick — the same reason the sibling specs pause here.
   */
  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 0));
    appRef.tick();
  };

  /** Points the store at a coffee and answers the three reads it then issues. */
  async function load(reviews: Review[] = [], c: Coffee = coffee()): Promise<CoffeeDetailStore> {
    const store = TestBed.inject(CoffeeDetailStore);
    store.setCoffeeId(7);
    appRef.tick();

    http.expectOne('/api/coffees/7').flush(c);
    http.expectOne('/api/coffees/7/reviews').flush(reviews);
    http.expectOne('/api/flavor-tags').flush([{ id: 1, name: 'Berry' }]);
    // The catalog store this one injects fetches the shelf on its own.
    http.match('/api/coffees').forEach((r) => r.flush([c]));
    await settle();
    return store;
  }

  it('reads the coffee, its reviews and the tag list for the id it is given', async () => {
    const store = await load([review({ id: 1, userId: ME })]);

    expect(store.coffee()?.name).toBe('Kirinyaga AA');
    expect(store.reviews()).toHaveLength(1);
    expect(store.tags()).toEqual([{ id: 1, name: 'Berry' }]);
    expect(store.loading()).toBe(false);
  });

  it('splits reviews into mine and everyone else’s', async () => {
    const store = await load([
      review({ id: 1, userId: ME }),
      review({ id: 2, userId: SOMEONE_ELSE }),
      review({ id: 3, userId: ME }),
    ]);

    expect(store.myReviews().map((r) => r.id)).toEqual([1, 3]);
    expect(store.otherReviews().map((r) => r.id)).toEqual([2]);
  });

  it('renders a not-found state instead of throwing when the coffee is gone', async () => {
    const store = TestBed.inject(CoffeeDetailStore);
    store.setCoffeeId(404);
    appRef.tick();

    http.expectOne('/api/coffees/404').flush('nope', { status: 404, statusText: 'Not Found' });
    http.expectOne('/api/coffees/404/reviews').flush([]);
    http.expectOne('/api/flavor-tags').flush([]);
    http.match('/api/coffees').forEach((r) => r.flush([]));
    await settle();

    // A resource's value() throws while errored; reading it must not take the screen
    // down with it.
    expect(() => store.coffee()).not.toThrow();
    expect(store.coffee()).toBeUndefined();
    expect(store.notFound()).toBe(true);
  });

  it('keeps showing the skeleton rather than the previous coffee while the next one loads', async () => {
    const store = await load([], coffee({ id: 7, name: 'Kirinyaga AA' }));
    expect(store.loading()).toBe(false);

    store.setCoffeeId(8);
    appRef.tick();

    // Mid-flight: the old coffee is still in the resource, but it is not the one the
    // route now asks for, so the screen must not render it as if it were.
    expect(store.loading()).toBe(true);

    http.expectOne('/api/coffees/8').flush(coffee({ id: 8, name: 'Yirgacheffe Konga' }));
    http.expectOne('/api/coffees/8/reviews').flush([]);
    await settle();

    expect(store.loading()).toBe(false);
    expect(store.coffee()?.name).toBe('Yirgacheffe Konga');
  });

  // The screen hands over its id *signal*, because the router reuses the component when
  // only the route parameter changes. Reading the id once — in a store onInit, or an
  // ngOnInit — would leave the screen on the previous coffee forever.
  it('follows the id signal it was given rather than reading it once', async () => {
    const routeId = signal(7);
    const store = TestBed.inject(CoffeeDetailStore);
    store.setCoffeeId(routeId);
    appRef.tick();

    http.expectOne('/api/coffees/7').flush(coffee({ id: 7 }));
    http.expectOne('/api/coffees/7/reviews').flush([]);
    http.expectOne('/api/flavor-tags').flush([]);
    http.match('/api/coffees').forEach((r) => r.flush([]));
    await settle();
    expect(store.coffee()?.id).toBe(7);

    routeId.set(8);
    appRef.tick();

    http.expectOne('/api/coffees/8').flush(coffee({ id: 8, name: 'Yirgacheffe Konga' }));
    http.expectOne('/api/coffees/8/reviews').flush([]);
    await settle();

    expect(store.coffee()?.name).toBe('Yirgacheffe Konga');
  });

  it('toggles a flavour tag on and back off', async () => {
    const store = await load();

    expect(store.isTagOn(1)).toBe(false);
    store.toggleTag(1);
    expect(store.isTagOn(1)).toBe(true);
    store.toggleTag(1);
    expect(store.isTagOn(1)).toBe(false);
  });

  it('posts the rating with its notes and tags, then clears the form', async () => {
    const store = await load();
    store.setRating(5);
    store.setStage('Fresh bag');
    store.setNotes('Blackcurrant and grapefruit.');
    store.toggleTag(1);

    store.rate();
    const posted = http.expectOne('/api/coffees/7/reviews');
    expect(posted.request.body).toEqual({
      rating: 5,
      stage: 'Fresh bag',
      tastingNotes: 'Blackcurrant and grapefruit.',
      tagIds: [1],
    });

    posted.flush(review({ id: 9, userId: ME, rating: 5 }));
    await settle();

    expect(store.saving()).toBe(false);
    expect(store.rating()).toBe(0);
    expect(store.stage()).toBe('');
    expect(store.notes()).toBe('');
    expect(store.isTagOn(1)).toBe(false);
  });

  it('sends empty optional fields as null rather than empty strings', async () => {
    const store = await load();
    store.setRating(3);

    store.rate();
    const posted = http.expectOne('/api/coffees/7/reviews');

    expect(posted.request.body).toMatchObject({ stage: null, tastingNotes: null });
  });

  it('keeps what the user typed when saving fails, so it can be retried', async () => {
    const store = await load();
    store.setRating(4);
    store.setNotes('Worth keeping.');

    store.rate();
    http.expectOne('/api/coffees/7/reviews').flush('no', { status: 500, statusText: 'Error' });
    await settle();

    expect(store.saving()).toBe(false);
    expect(store.rating()).toBe(4);
    expect(store.notes()).toBe('Worth keeping.');
  });

  // exhaustMap, not switchMap: a double-clicked Save must post one rating, not two.
  it('ignores a second save while the first is still in flight', async () => {
    const store = await load();
    store.setRating(4);

    store.rate();
    store.rate();

    expect(http.match('/api/coffees/7/reviews')).toHaveLength(1);
  });

  it('arms and disarms the delete confirmation', async () => {
    const store = await load();

    expect(store.confirmingDelete()).toBe(false);
    store.armDelete();
    expect(store.confirmingDelete()).toBe(true);
    store.cancelDelete();
    expect(store.confirmingDelete()).toBe(false);
  });

  it('deletes the coffee and stands the confirmation down afterwards', async () => {
    const store = await load();
    store.armDelete();

    store.confirmDelete();
    http.expectOne({ method: 'DELETE', url: '/api/coffees/7' }).flush(null);
    await settle();

    expect(store.deleting()).toBe(false);
    expect(store.confirmingDelete()).toBe(false);
  });

  it('stands the confirmation down when the delete is refused', async () => {
    const store = await load();
    store.armDelete();

    store.confirmDelete();
    http
      .expectOne({ method: 'DELETE', url: '/api/coffees/7' })
      .flush('forbidden', { status: 403, statusText: 'Forbidden' });
    await settle();

    // Left armed and disabled, the screen would strand the user on a confirm row whose
    // buttons no longer do anything.
    expect(store.deleting()).toBe(false);
    expect(store.confirmingDelete()).toBe(false);
  });
});

import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { ApplicationRef, provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ToastService } from '@coffee-tracker/ui';
import { CoffeeFormStore } from './coffee-form.store';

/**
 * The add/edit store. Its twin, CoffeeDetailStore, is covered; this one was not, and it
 * owns the two places where a mistake costs the user something real: a double submit
 * that creates two coffees, and a photo upload that fails after the coffee is already
 * saved.
 */

// CoffeesStore keys its resource on the signed-in user, and this store injects it.
function seedSession(): void {
  localStorage.setItem(
    'ct.session',
    JSON.stringify({
      token: 't',
      userId: 'me-1',
      displayName: 'Tester',
      isAdmin: false,
      expiresAt: new Date(Date.now() + 900_000).toISOString(),
    }),
  );
}

describe('CoffeeFormStore', () => {
  let http: HttpTestingController;
  let appRef: ApplicationRef;
  let toast: { show: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    seedSession();
    toast = { show: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'coffees/:id', children: [] }]),
        { provide: ToastService, useValue: toast },
        CoffeeFormStore,
      ],
    });
    http = TestBed.inject(HttpTestingController);
    appRef = TestBed.inject(ApplicationRef);
  });

  afterEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  /** rxResource publishes on a macrotask, so settling takes more than a tick. */
  const settle = async (): Promise<void> => {
    await new Promise((resolve) => setTimeout(resolve, 0));
    appRef.tick();
  };

  function newStore(): CoffeeFormStore {
    const store = TestBed.inject(CoffeeFormStore);
    appRef.tick();
    // The catalog store this one injects fetches the shelf on its own.
    http.match('/api/coffees').forEach((r) => r.flush([]));
    return store;
  }

  function fill(store: CoffeeFormStore): void {
    store.model.update((m) => ({
      ...m,
      name: 'Kirinyaga AA',
      roaster: 'La Cabra',
      origin: 'Kenya',
    }));
  }

  it('creates a coffee and sends the user to it', async () => {
    const store = newStore();
    fill(store);

    store.save({ id: null, file: null });
    const posted = http.expectOne({ method: 'POST', url: '/api/coffees' });
    expect(posted.request.body).toMatchObject({ name: 'Kirinyaga AA', roaster: 'La Cabra' });

    posted.flush({ id: 7 });
    await settle();

    expect(store.submitAction.running()).toBe(false);
    expect(toast.show).toHaveBeenCalledWith('Coffee added.', 'success');
  });

  it('updates an existing coffee rather than creating a second one', async () => {
    const store = newStore();
    fill(store);

    store.save({ id: 7, file: null });

    http.expectOne({ method: 'PUT', url: '/api/coffees/7' }).flush(null);
    http.expectNone({ method: 'POST', url: '/api/coffees' });
    await settle();

    expect(toast.show).toHaveBeenCalledWith('Coffee updated.', 'success');
  });

  // exhaustMap, not switchMap: the reason is that cancel-and-restart would let a
  // double-clicked Save create two coffees.
  it('ignores a second submit while the first is in flight', () => {
    const store = newStore();
    fill(store);

    store.save({ id: null, file: null });
    store.save({ id: null, file: null });

    expect(http.match({ method: 'POST', url: '/api/coffees' })).toHaveLength(1);
  });

  it('uploads the photo after the coffee exists, not before', async () => {
    const store = newStore();
    fill(store);
    const file = new File(['bytes'], 'bag.png', { type: 'image/png' });

    store.save({ id: null, file });

    // Nothing to attach a photo to until the coffee has an id.
    http.expectNone({ method: 'POST', url: '/api/coffees/7/photo' });
    http.expectOne({ method: 'POST', url: '/api/coffees' }).flush({ id: 7 });
    await settle();

    http.expectOne({ method: 'POST', url: '/api/coffees/7/photo' }).flush({ id: 7 });
    await settle();

    expect(toast.show).toHaveBeenCalledWith('Coffee added.', 'success');
  });

  // The coffee is already saved by then. Reporting it as a failure would invite a
  // resubmit, and the resubmit would create a duplicate.
  it('keeps a saved coffee when only its photo fails to upload', async () => {
    const store = newStore();
    fill(store);
    const file = new File(['bytes'], 'bag.png', { type: 'image/png' });

    store.save({ id: null, file });
    http.expectOne({ method: 'POST', url: '/api/coffees' }).flush({ id: 7 });
    await settle();

    // The coffee is saved but the command is not done: the upload is the slow leg, and a
    // button that goes idle here invites a second submit with no feedback.
    expect(store.submitAction.running()).toBe(true);

    http
      .expectOne({ method: 'POST', url: '/api/coffees/7/photo' })
      .flush('nope', { status: 500, statusText: 'Error' });
    await settle();

    expect(toast.show).toHaveBeenCalledWith(
      expect.stringMatching(/saved, but the photo failed/i),
      'error',
    );
    expect(store.submitAction.running()).toBe(false);
  });

  it('keeps the user on the form when the save itself fails', async () => {
    const store = newStore();
    fill(store);

    store.save({ id: null, file: null });
    http
      .expectOne({ method: 'POST', url: '/api/coffees' })
      .flush('nope', { status: 500, statusText: 'Error' });
    await settle();

    expect(toast.show).toHaveBeenCalledWith('Could not save the coffee.', 'error');
    // Released, so the user can correct and retry rather than facing a dead button.
    expect(store.submitAction.running()).toBe(false);
    expect(store.model().name).toBe('Kirinyaga AA');
  });

  it('loads an existing coffee into the form for editing', async () => {
    const store = newStore();

    store.load(7);
    appRef.tick();
    http.expectOne('/api/coffees/7').flush({
      id: 7,
      name: 'Kirinyaga AA',
      roaster: 'La Cabra',
      origin: 'Kenya',
      roastLevel: 'Light',
      price: 18.5,
      dateBought: '2026-08-28',
      photoUrl: '/photos/bag.webp?sig=x',
      shopName: null,
      purchaseUrl: null,
      createdAt: '2026-08-28T00:00:00Z',
      averageRating: null,
      reviewCount: 0,
      flavorTags: [],
    });
    await settle();

    expect(store.model().name).toBe('Kirinyaga AA');
    expect(store.loading()).toBe(false);
    // The screen previews the photo already attached, so it must survive the load.
    expect(store.photoUrl()).toBe('/photos/bag.webp?sig=x');
  });

  it('says so when the coffee being edited cannot be loaded', async () => {
    const store = newStore();

    store.load(404);
    appRef.tick();
    http.expectOne('/api/coffees/404').flush('gone', { status: 404, statusText: 'Not Found' });
    await settle();

    expect(store.loading()).toBe(false);
    expect(toast.show).toHaveBeenCalledWith('Could not load that coffee.', 'error');
  });
});

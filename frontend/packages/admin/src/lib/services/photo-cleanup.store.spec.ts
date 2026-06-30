import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { PhotoCleanupStore } from './photo-cleanup.store';

const SEED = [
  { path: 'photos/used.jpg', used: true },
  { path: 'photos/orphan1.jpg', used: false },
  { path: 'photos/orphan2.jpg', used: false },
];

describe('PhotoCleanupStore', () => {
  let store: PhotoCleanupStore;
  let http: HttpTestingController;
  let appRef: ApplicationRef;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), PhotoCleanupStore],
    });
    store = TestBed.inject(PhotoCleanupStore);
    http = TestBed.inject(HttpTestingController);
    appRef = TestBed.inject(ApplicationRef);

    // httpResource issues its GET from a reactive effect — tick() runs it, then we
    // flush the seed and tick() again so the value lands in the resource signal.
    appRef.tick();
    http.expectOne('/api/admin/photos').flush(SEED);
    appRef.tick();
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  it('counts stored vs unused', () => {
    expect(store.storedCount()).toBe(3);
    expect(store.unusedCount()).toBe(2);
  });

  it('toggles selection and reports the count', () => {
    store.toggle('photos/orphan1.jpg');
    expect(store.isSelected('photos/orphan1.jpg')).toBe(true);
    expect(store.selectedCount()).toBe(1);
    store.toggle('photos/orphan1.jpg');
    expect(store.selectedCount()).toBe(0);
  });

  it('selects all unused (never the used one)', () => {
    store.selectAllUnused();
    expect(store.selectedCount()).toBe(2);
    expect(store.isSelected('photos/used.jpg')).toBe(false);
  });

  it('filters to unused only', () => {
    expect(store.visible().length).toBe(3);
    store.setFilter('unused');
    expect(store.visible().length).toBe(2);
    expect(store.visible().every((p) => !p.used)).toBe(true);
  });

  it('deletes the selection, then clears it and refetches', async () => {
    store.selectAllUnused();
    const done = store.deleteSelected();

    const del = http.expectOne('/api/admin/photos');
    expect(del.request.method).toBe('DELETE');
    expect(del.request.body).toEqual({ paths: ['photos/orphan1.jpg', 'photos/orphan2.jpg'] });
    del.flush({ deleted: 2, skipped: 0 });

    // Let deleteSelected resume past its `await` (→ clearSelection + reload), then tick
    // so the reload's httpResource effect issues a fresh GET we can satisfy.
    await Promise.resolve();
    appRef.tick();
    http.expectOne('/api/admin/photos').flush([{ path: 'photos/used.jpg', used: true }]);

    const result = await done;
    expect(result).toEqual({ deleted: 2, skipped: 0 });
    expect(store.selectedCount()).toBe(0);
  });
});

describe('PhotoCleanupStore (error path)', () => {
  let store: PhotoCleanupStore;
  let http: HttpTestingController;
  let appRef: ApplicationRef;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), PhotoCleanupStore],
    });
    store = TestBed.inject(PhotoCleanupStore);
    http = TestBed.inject(HttpTestingController);
    appRef = TestBed.inject(ApplicationRef);

    appRef.tick();
    http.expectOne('/api/admin/photos').flush('boom', { status: 500, statusText: 'Server Error' });
    appRef.tick();
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  it('surfaces a friendly error, stops loading, and exposes an empty list without throwing', () => {
    expect(store.error()).toBe('Could not load stored photos.');
    expect(store.loading()).toBe(false);
    // The raw httpResource value rethrows in the error state; the store guards it.
    expect(store.photos()).toEqual([]);
    expect(store.visible()).toEqual([]);
    expect(store.storedCount()).toBe(0);
  });
});

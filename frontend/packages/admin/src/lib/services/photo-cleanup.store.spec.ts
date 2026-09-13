import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ToastService } from '@coffee-tracker/ui';
import { PhotoCleanupStore } from './photo-cleanup.store';

const SEED = [
  { path: 'photos/used.jpg', url: '/photos/used.jpg?exp=1&sig=a', used: true },
  { path: 'photos/orphan1.jpg', url: '/photos/orphan1.jpg?exp=1&sig=b', used: false },
  { path: 'photos/orphan2.jpg', url: '/photos/orphan2.jpg?exp=1&sig=c', used: false },
];

describe('PhotoCleanupStore', () => {
  let store: PhotoCleanupStore;
  let http: HttpTestingController;
  let appRef: ApplicationRef;
  let toast: { show: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    toast = { show: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toast },
        PhotoCleanupStore,
      ],
    });
    store = TestBed.inject(PhotoCleanupStore);
    http = TestBed.inject(HttpTestingController);
    appRef = TestBed.inject(ApplicationRef);

    // The resource issues its GET from a reactive effect — tick() runs it, then we
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

  it('deletes the selection, then clears it and refetches', () => {
    store.selectAllUnused();
    store.deleteSelected();
    expect(store.pending()).toBe(true);

    const del = http.expectOne('/api/admin/photos');
    expect(del.request.method).toBe('DELETE');
    expect(del.request.body).toEqual({ paths: ['photos/orphan1.jpg', 'photos/orphan2.jpg'] });
    // tapResponse runs on the flush itself, not on a later microtask, so the outcome and
    // the reload are both in place by the time this returns.
    del.flush({ deleted: 2, skipped: 0 });

    appRef.tick();
    http
      .expectOne('/api/admin/photos')
      .flush([{ path: 'photos/used.jpg', url: '/photos/used.jpg?exp=1&sig=a', used: true }]);

    expect(toast.show).toHaveBeenCalledWith('Deleted 2, skipped 0', 'success');
    expect(store.selectedCount()).toBe(0);
    expect(store.confirming()).toBe(false);
    expect(store.pending()).toBe(false);
  });

  it('reports a failed delete without clearing the selection', () => {
    store.selectAllUnused();
    store.deleteSelected();

    http
      .expectOne('/api/admin/photos')
      .flush('boom', { status: 500, statusText: 'Server Error' });

    expect(toast.show).toHaveBeenCalledWith('Delete failed — please retry.', 'error');
    expect(store.requestError()).toBe('Delete failed — please retry.');
    expect(store.pending()).toBe(false);
    // The selection survives, so the operator can retry without re-picking.
    expect(store.selectedCount()).toBe(2);
  });
});

describe('PhotoCleanupStore (error path)', () => {
  let store: PhotoCleanupStore;
  let http: HttpTestingController;
  let appRef: ApplicationRef;
  let toast: { show: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    toast = { show: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toast },
        PhotoCleanupStore,
      ],
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
    // A resource value() rethrows in the error state; withValueOnError answers [].
    expect(store.photos()).toEqual([]);
    expect(store.visible()).toEqual([]);
    expect(store.storedCount()).toBe(0);
  });
});

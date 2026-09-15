import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ToastService } from '@coffee-tracker/ui';
import { CoffeesStore } from '@coffee-tracker/coffees';
import { BackupStore } from './backup.store';
import type { Backup } from '@coffee-tracker/data';

const VALID: Backup = {
  formatVersion: 1,
  exportedAt: '2026-09-13T12:00:00+00:00',
  coffees: [],
};

/** A `File` whose `text()` resolves to whatever the test wants to have been chosen. */
function fileOf(contents: string, name = 'backup.json'): File {
  return { name, text: () => Promise.resolve(contents) } as File;
}

describe('BackupStore', () => {
  let store: BackupStore;
  let http: HttpTestingController;
  let toast: { show: ReturnType<typeof vi.fn> };
  let catalog: { reload: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    toast = { show: vi.fn() };
    catalog = { reload: vi.fn() };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toast },
        { provide: CoffeesStore, useValue: catalog },
        BackupStore,
      ],
    });
    store = TestBed.inject(BackupStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  /** Stages a file and asserts it was accepted, so the restore tests can start armed. */
  async function stage(backup: Backup = VALID): Promise<void> {
    await store.choose(fileOf(JSON.stringify(backup)));
    expect(store.staged()).not.toBeNull();
  }

  describe('export', () => {
    it('hands the fetched catalog to the browser as a download', () => {
      // jsdom has no object URLs and no navigation, so the two ends of the download are
      // the only part that can be observed: a blob was made, and a link was clicked.
      const createObjectURL = vi.fn(() => 'blob:x');
      const revokeObjectURL = vi.fn();
      vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL });
      const click = vi
        .spyOn(HTMLAnchorElement.prototype, 'click')
        .mockImplementation(() => undefined);

      store.exportCatalog();
      http.expectOne({ method: 'GET', url: '/api/admin/backup' }).flush(VALID);

      expect(createObjectURL).toHaveBeenCalledOnce();
      expect(click).toHaveBeenCalledOnce();
      expect(store.exporting()).toBe(false);
      vi.unstubAllGlobals();
      click.mockRestore();
    });

    it('reports a failed export instead of leaving the button spinning', () => {
      store.exportCatalog();
      http.expectOne('/api/admin/backup').flush('nope', { status: 500, statusText: 'Error' });

      expect(store.exporting()).toBe(false);
      expect(toast.show).toHaveBeenCalledWith(expect.stringContaining('export'), 'error');
    });
  });

  describe('choosing a file', () => {
    it('stages a backup with its name, and sends nothing yet', async () => {
      await store.choose(fileOf(JSON.stringify(VALID), 'shelf.json'));

      expect(store.staged()?.fileName).toBe('shelf.json');
      // The point of staging: nothing destructive happens until the user confirms.
      http.expectNone('/api/admin/backup');
    });

    it('refuses a file that is not JSON', async () => {
      await store.choose(fileOf('<html>not this</html>'));

      expect(store.staged()).toBeNull();
      expect(toast.show).toHaveBeenCalledWith(expect.stringContaining('JSON'), 'error');
    });

    it('refuses JSON that is not a backup', async () => {
      // Valid JSON from some other export would otherwise reach the API as a 400 the
      // user has to interpret; the shape is checkable here.
      await store.choose(fileOf(JSON.stringify({ items: [] })));

      expect(store.staged()).toBeNull();
      expect(toast.show).toHaveBeenCalledWith(expect.stringContaining('backup'), 'error');
    });

    it('clears the previous result, so an old count cannot be read as the new one', async () => {
      await stage();
      store.confirmImport();
      http.expectOne('/api/admin/backup').flush({ coffees: 2, reviews: 3, warnings: [] });
      expect(store.lastResult()).not.toBeNull();

      await stage();

      expect(store.lastResult()).toBeNull();
    });

    it('cancelling drops the staged file', async () => {
      await stage();

      store.cancel();

      expect(store.staged()).toBeNull();
    });
  });

  describe('restoring', () => {
    it('posts the staged backup and reports what was written', async () => {
      await stage();

      store.confirmImport();
      const req = http.expectOne({ method: 'POST', url: '/api/admin/backup' });
      expect(req.request.body).toEqual(VALID);
      req.flush({ coffees: 2, reviews: 5, warnings: ['Unknown flavour tag "Smoky" was skipped.'] });

      expect(store.lastResult()?.coffees).toBe(2);
      expect(store.lastResult()?.warnings).toHaveLength(1);
      expect(store.staged()).toBeNull();
      expect(store.importing()).toBe(false);
      expect(toast.show).toHaveBeenCalledWith('Catalog restored.', 'success');
    });

    it('reloads the catalog, which is now a different catalog', async () => {
      await stage();

      store.confirmImport();
      http.expectOne('/api/admin/backup').flush({ coffees: 1, reviews: 0, warnings: [] });

      expect(catalog.reload).toHaveBeenCalledOnce();
    });

    it('surfaces the API reason rather than a generic failure', async () => {
      await stage({ ...VALID, formatVersion: 2 });

      store.confirmImport();
      http.expectOne('/api/admin/backup').flush(
        { detail: 'This backup is version 2; this instance reads version 1.' },
        { status: 400, statusText: 'Bad Request' },
      );

      expect(toast.show).toHaveBeenCalledWith(expect.stringContaining('version 2'), 'error');
      expect(store.lastResult()).toBeNull();
      expect(catalog.reload).not.toHaveBeenCalled();
    });

    it('falls back to its own message when the API sends no reason', async () => {
      await stage();

      store.confirmImport();
      http.expectOne('/api/admin/backup').flush(null, { status: 500, statusText: 'Error' });

      expect(toast.show).toHaveBeenCalledWith(expect.stringContaining('refused'), 'error');
      expect(store.staged()).toBeNull();
    });

    it('a second confirm while one is in flight does not post twice', async () => {
      await stage();

      store.confirmImport();
      store.confirmImport();

      // exhaustMap: the confirm button is disabled while importing, and a double
      // submit that slipped through would otherwise replace the catalog twice.
      http.expectOne('/api/admin/backup').flush({ coffees: 0, reviews: 0, warnings: [] });
    });
  });
});

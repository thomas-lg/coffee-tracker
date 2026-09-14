import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ToastService } from '@coffee-tracker/ui';
import type { Backup } from '@coffee-tracker/data';
import { BackupScreen } from './backup';

const BACKUP: Backup = {
  formatVersion: 1,
  exportedAt: '2026-01-31T10:00:00Z',
  coffees: [],
};

/**
 * `backup.store.spec.ts` covers parsing, refusing and the two API calls. What is left is
 * what only the rendered screen can answer: a restore deletes the whole catalog, so the
 * assertions here are that choosing a file sends nothing on its own, and that focus
 * follows the confirm row the pick brings into existence.
 */
describe('BackupScreen', () => {
  let fixture: ReturnType<typeof TestBed.createComponent<BackupScreen>>;
  let http: HttpTestingController;
  let toast: { show: ReturnType<typeof vi.fn> };

  const el = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const fileInput = (): HTMLInputElement => el().querySelector('input[type=file]')!;
  const buttonSaying = (text: RegExp): HTMLElement | undefined =>
    Array.from(el().querySelectorAll<HTMLElement>('button')).find((b) => text.test(b.textContent ?? ''));

  /** A File whose text() resolves to `content`; jsdom's File does not implement it. */
  function fileOf(content: string, name = 'backup.json'): File {
    const file = new File([content], name, { type: 'application/json' });
    Object.defineProperty(file, 'text', { value: () => Promise.resolve(content) });
    return file;
  }

  /** Drives the hidden file input the way the browser does after a pick. */
  async function pick(file: File): Promise<void> {
    const input = fileInput();
    Object.defineProperty(input, 'files', { value: [file], configurable: true });
    input.dispatchEvent(new Event('change'));
    await settle();
  }

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  beforeEach(() => {
    toast = { show: vi.fn() };
    TestBed.configureTestingModule({
      imports: [BackupScreen],
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toast },
      ],
    });
    fixture = TestBed.createComponent(BackupScreen);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  it('sends nothing when a file is chosen, only arms the confirmation', async () => {
    await pick(fileOf(JSON.stringify({ ...BACKUP, coffees: [{ name: 'Kekchi' }] })));

    // http.verify() in afterEach is the other half of this: any request here fails it.
    expect(el().textContent).toContain('Replace the catalog with 1 coffee(s)');
    expect(el().textContent).toContain('backup.json');
  });

  it('moves focus to Cancel once armed, because the button that had it is gone', async () => {
    // Staging swaps the picker for the confirm row, so focus would otherwise drop to
    // <body> and a keyboard user would land on a destructive button with no warning.
    await pick(fileOf(JSON.stringify(BACKUP)));

    expect(document.activeElement?.textContent?.trim()).toBe('Cancel');
  });

  it('clears the control so the same file can be chosen twice', async () => {
    // `change` does not fire for an identical value, so a refused file that stayed named
    // in the input could not be retried after fixing it.
    await pick(fileOf('nonsense'));

    expect(fileInput().value).toBe('');
  });

  it('posts the parsed backup only after the confirmation, and reports what it wrote', async () => {
    await pick(fileOf(JSON.stringify(BACKUP)));

    buttonSaying(/Replace catalog/)?.click();
    const req = http.expectOne({ method: 'POST', url: '/api/admin/backup' });
    expect(req.request.body).toEqual(BACKUP);
    req.flush({ coffees: 3, reviews: 7, warnings: ['Dropped an unknown tag'] });
    await settle();

    expect(el().textContent).toContain('Restored 3 coffee(s) and 7 review(s)');
    expect(el().textContent).toContain('Dropped an unknown tag');
    expect(toast.show).toHaveBeenCalledWith(expect.stringContaining('restored'), 'success');
  });

  it('cancelling disarms it, and the catalog is never touched', async () => {
    await pick(fileOf(JSON.stringify(BACKUP)));

    buttonSaying(/Cancel/)?.click();
    await settle();

    expect(el().textContent).not.toContain('Replace the catalog');
    expect(el().textContent).toContain('Choose a backup file');
  });
});

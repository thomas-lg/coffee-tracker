import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ToastService } from '@coffee-tracker/ui';
import type { ScanSettings } from '@coffee-tracker/data';
import { ScanSettingsScreen } from './scan-settings';

const SETTINGS: ScanSettings = {
  engine: 'RapidOcr',
  options: [
    { engine: 'RapidOcr', available: true },
    { engine: 'Tesseract', available: true },
    { engine: 'Disabled', available: true },
  ],
};

describe('ScanSettingsScreen', () => {
  let fixture: ReturnType<typeof TestBed.createComponent<ScanSettingsScreen>>;
  let http: HttpTestingController;
  let toast: { show: ReturnType<typeof vi.fn> };

  const radios = (): HTMLInputElement[] =>
    Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>('input[type=radio]'),
    );

  beforeEach(() => {
    toast = { show: vi.fn() };
    TestBed.configureTestingModule({
      imports: [ScanSettingsScreen],
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: toast },
      ],
    });
    fixture = TestBed.createComponent(ScanSettingsScreen);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    try {
      http.verify();
    } finally {
      // In a finally so a failed verify cannot leave the module instantiated: the next
      // spec file in the same worker would then fail to configure its own TestBed and
      // bury the real failure.
      TestBed.resetTestingModule();
    }
  });

  /**
   * Lets pending promises run and re-renders. Deliberately not whenStable(): that waits
   * for outstanding HTTP, and here the test is the thing answering it, which deadlocks.
   */
  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  async function load(settings: ScanSettings = SETTINGS): Promise<void> {
    http.expectOne({ method: 'GET', url: '/api/admin/scan-settings' }).flush(settings);
    await settle();
  }

  it('offers every engine and marks the one in use', async () => {
    await load();

    expect(radios()).toHaveLength(3);
    expect(radios()[0]?.checked).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('In use');
  });

  it('explains what each engine is for, since that is the point of the screen', async () => {
    await load();

    const text = fixture.nativeElement.textContent ?? '';
    expect(text).toContain('RapidOCR');
    expect(text).toContain('Tesseract');
    // The trade-off, not just the names.
    expect(text).toMatch(/faster/i);
    expect(text).toMatch(/container image/i);
  });

  it('sends the chosen engine and keeps what came back', async () => {
    await load();

    radios()[1]?.click();
    // The request waits for one render, so the group is on screen with the click applied
    // before anything can answer it. Without settling here there is no PUT to expect.
    await settle();
    const req = http.expectOne({ method: 'PUT', url: '/api/admin/scan-settings' });
    expect(req.request.body).toEqual({ engine: 'Tesseract' });
    req.flush({ ...SETTINGS, engine: 'Tesseract' });
    await settle();

    expect(radios()[1]?.checked).toBe(true);
    expect(toast.show).toHaveBeenCalledWith(expect.stringContaining('changed'), 'success');
  });

  it('cannot select an engine this host does not carry', async () => {
    // A build without one of them must not let an administrator pick it and then meet a
    // 503 on every scan.
    await load({ ...SETTINGS, options: [
      { engine: 'RapidOcr', available: true },
      { engine: 'Tesseract', available: false },
      { engine: 'Disabled', available: true },
    ] });

    expect(radios()[1]?.disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Not installed on this host');
  });

  it('reports a refused change and puts the selection back', async () => {
    await load();

    radios()[1]?.click();
    await settle();
    // The browser has already moved the selection; nothing has confirmed it yet.
    expect(radios()[1]?.checked).toBe(true);

    http.expectOne('/api/admin/scan-settings').flush('nope', { status: 500, statusText: 'Error' });
    await settle();

    expect(toast.show).toHaveBeenCalledWith(expect.stringContaining('Could not'), 'error');
    // Back on the engine actually in force. The form binding only rewrites a radio when
    // the model's value changes, so a refusal that arrives with no render in between has
    // nothing to write and the DOM keeps the click: drop the settle() above and this goes
    // red. choose() waits for that render rather than leaving it to the scheduler, which
    // is the part this test can require but not itself provoke.
    expect(radios()[0]?.checked).toBe(true);
    expect(radios()[1]?.checked).toBe(false);
  });
});

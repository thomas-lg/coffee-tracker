import { describe, expect, it, beforeEach, afterEach, vi } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ToastService } from '@coffee-tracker/ui';
import type { PhotoListItem } from '@coffee-tracker/data';
import { PhotoCleanup } from './photo-cleanup';

const PHOTOS: PhotoListItem[] = [
  { path: 'photos/used-one.jpg', url: '/p/used-one.jpg', used: true },
  { path: 'photos/orphan-a.jpg', url: '/p/orphan-a.jpg', used: false },
  { path: 'photos/orphan-b.jpg', url: '/p/orphan-b.jpg', used: false },
];

/**
 * `photo-cleanup.store.spec.ts` covers selection, filtering and the delete call. What is
 * left is what only the rendered screen can answer: that a photo still attached to a
 * coffee cannot be picked for deletion, that the selection reaches a screen reader as
 * something other than a ring colour, and that focus survives the two-step delete
 * destroying the button it was on.
 */
describe('PhotoCleanup', () => {
  let fixture: ReturnType<typeof TestBed.createComponent<PhotoCleanup>>;
  let http: HttpTestingController;

  const el = (): HTMLElement => fixture.nativeElement as HTMLElement;
  /** The photo tiles: the only buttons carrying an aria-label. */
  const tiles = (): HTMLButtonElement[] =>
    Array.from(el().querySelectorAll<HTMLButtonElement>('button[aria-label]'));
  const click = (label: string): void => {
    const button = Array.from(el().querySelectorAll<HTMLElement>('button')).find(
      (b) => b.textContent?.trim() === label,
    );
    expect(button, `no button labelled "${label}"`).toBeTruthy();
    button?.click();
  };

  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  async function load(photos: PhotoListItem[] = PHOTOS): Promise<void> {
    http.expectOne({ method: 'GET', url: '/api/admin/photos' }).flush(photos);
    await settle();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [PhotoCleanup],
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: { show: vi.fn() } },
      ],
    });
    fixture = TestBed.createComponent(PhotoCleanup);
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  it('locks a photo that is still attached to a coffee', async () => {
    // The API skips used photos whatever is sent it; disabling the tile is what turns
    // that guarantee into something the screen says rather than a silent no-op.
    await load();

    expect(tiles()[0]?.disabled).toBe(true);
    expect(tiles()[0]?.getAttribute('aria-label')).toContain('In use by a coffee');
    expect(tiles()[1]?.disabled).toBe(false);
  });

  it('carries the selection in aria-pressed, not only in the ring colour', async () => {
    // The tick is aria-hidden and the ring is a colour change, so without this a screen
    // reader user can select photos and not be able to tell what they are deleting.
    await load();

    expect(tiles()[1]?.getAttribute('aria-pressed')).toBe('false');

    tiles()[1]?.click();
    await settle();

    expect(tiles()[1]?.getAttribute('aria-pressed')).toBe('true');
    // A locked tile is not a pressable thing at all, so it carries no state.
    expect(tiles()[0]?.getAttribute('aria-pressed')).toBeNull();
  });

  it('shows the confirm step before anything is sent', async () => {
    await load();

    tiles()[1]?.click();
    await settle();
    click('Delete 1');
    await settle();

    // http.verify() in afterEach is the assertion that nothing was sent.
    expect(el().textContent).toContain('Delete 1 photo(s)?');
  });

  it('moves focus onto Cancel when arming, and back to the action when cancelling', async () => {
    // Either transition destroys the button that had focus, which would otherwise drop
    // to <body> and leave a keyboard user at the top of the document.
    await load();

    tiles()[1]?.click();
    await settle();
    click('Delete 1');
    await settle();

    expect(document.activeElement?.textContent?.trim()).toBe('Cancel');

    click('Cancel');
    await settle();

    expect(document.activeElement?.textContent?.trim()).toBe('Delete 1');
  });

  it('does not grab focus merely because the screen rendered', async () => {
    // The focus effect runs on first render too; acting there would yank a reader out of
    // the heading and into a toolbar it never asked for.
    await load();

    expect(document.activeElement).toBe(document.body);
  });
});

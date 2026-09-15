import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import type { Coffee } from '@coffee-tracker/data';
import { CoffeeDetail } from './coffee-detail';

const ME = 'me-1';

const COFFEE: Coffee = {
  id: 7,
  name: 'Kirinyaga AA',
  roaster: 'La Cabra',
  origin: 'Kenya',
  roastLevel: 'Light',
  price: 18.5,
  dateBought: '2026-08-28',
  photoUrl: null,
  shopName: 'Beans & Co',
  purchaseUrl: null,
  createdAt: '2026-08-28T00:00:00Z',
  averageRating: 4.5,
  reviewCount: 2,
  flavorTags: [],
};

/**
 * `coffee-detail.store.spec.ts` covers loading, reviews and the delete call. What only
 * the rendered screen can answer is the spec table's shape and, mostly, that deleting a
 * coffee stays a two-step action a keyboard user can actually complete: both halves of
 * that swap out the element holding focus.
 */
describe('CoffeeDetail', () => {
  let fixture: ReturnType<typeof TestBed.createComponent<CoffeeDetail>>;
  let http: HttpTestingController;

  const el = (): HTMLElement => fixture.nativeElement as HTMLElement;
  const click = (label: string): void => {
    const button = Array.from(el().querySelectorAll<HTMLElement>('button')).find(
      (b) => b.textContent?.trim() === label,
    );
    expect(button, `no button labelled "${label}"`).toBeTruthy();
    button?.click();
  };

  /** rxResource publishes on a macrotask after the response, as the store spec notes. */
  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 0));
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  async function load(coffee: Coffee = COFFEE): Promise<void> {
    http.expectOne({ method: 'GET', url: `/api/coffees/${coffee.id}` }).flush(coffee);
    http.expectOne({ method: 'GET', url: `/api/coffees/${coffee.id}/reviews` }).flush([]);
    http.match({ method: 'GET', url: '/api/flavor-tags' }).forEach((r) => r.flush([]));
    await settle();
  }

  beforeEach(() => {
    // CoffeesStore keys its resource on the signed-in user, and this screen reaches it
    // through the detail store, so there has to be a session before TestBed builds.
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
    TestBed.configureTestingModule({
      imports: [CoffeeDetail],
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: 'coffees', children: [] }]),
      ],
    });
    fixture = TestBed.createComponent(CoffeeDetail);
    fixture.componentRef.setInput('id', '7');
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  it('lists the specs in display order', async () => {
    await load();

    expect(Array.from(el().querySelectorAll('dt')).map((dt) => dt.textContent?.trim())).toEqual([
      'Origin',
      'Roaster',
      'Roast',
      'Price',
      'Bought',
      'Shop',
    ]);
  });

  it('omits the shop row for a coffee that has not got one', async () => {
    // A second mount rather than a new id on this one: the store keys its resources on
    // the coffee id, and swapping the input mid-test races the refetch.
    const second = TestBed.createComponent(CoffeeDetail);
    second.componentRef.setInput('id', '8');
    second.detectChanges();
    http
      .expectOne({ method: 'GET', url: '/api/coffees/8' })
      .flush({ ...COFFEE, id: 8, shopName: null });
    http.expectOne({ method: 'GET', url: '/api/coffees/8/reviews' }).flush([]);
    await load();
    second.detectChanges();

    const terms = Array.from((second.nativeElement as HTMLElement).querySelectorAll('dt')).map(
      (dt) => dt.textContent?.trim(),
    );
    expect(terms).toContain('Roaster');
    expect(terms).not.toContain('Shop');
  });

  it('deletes nothing on the first click, only arms the confirmation', async () => {
    await load();

    click('Delete');
    await settle();

    // http.verify() is not used here (the resources stay open), so this is the assertion
    // that the first click is inert: the confirm branch is showing and nothing is gone.
    expect(el().textContent).toContain('Confirm delete');
    http.expectNone({ method: 'DELETE', url: '/api/coffees/7' });
  });

  it('moves focus onto Cancel when arming, and back to Delete when dismissing', async () => {
    // Each transition destroys the button that had focus. This was written against the
    // effect that used to do it, which read a viewChild the confirm branch had not
    // created yet and so focused nothing at all.
    await load();

    click('Delete');
    await settle();

    expect(document.activeElement?.textContent?.trim()).toBe('Cancel');

    click('Cancel');
    await settle();

    expect(document.activeElement?.textContent?.trim()).toBe('Delete');
  });

  it('does not grab focus merely because the page loaded', async () => {
    await load();

    expect(document.activeElement).toBe(document.body);
  });
  it('stands the confirm down and returns focus when the delete is refused', async () => {
    // The screen stays mounted on a refusal, and the confirm row's own buttons are
    // disabled by then, so nothing but this puts the user back somewhere usable.
    await load();

    click('Delete');
    await settle();
    click('Confirm delete');
    await settle();

    http
      .expectOne({ method: 'DELETE', url: '/api/coffees/7' })
      .flush('forbidden', { status: 403, statusText: 'Forbidden' });
    await settle();

    expect(el().textContent).not.toContain('Confirm delete');
    expect(document.activeElement?.textContent?.trim()).toBe('Delete');
  });
});

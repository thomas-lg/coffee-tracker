import { describe, expect, it, beforeEach, vi } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ToastService } from '@coffee-tracker/ui';
import type { AccountSettings } from '@coffee-tracker/data';
import { AccountSettingsScreen } from './account-settings';

// The screen that can lock an administrator out of their own instance. The refusal
// path matters more than the happy one: an admin who does not understand why a switch
// refused to move will reach for SQL.
describe('AccountSettingsScreen', () => {
  let fixture: ComponentFixture<AccountSettingsScreen>;
  let httpCtrl: HttpTestingController;
  let show: ReturnType<typeof vi.fn>;

  const SETTINGS: AccountSettings = { localLoginEnabled: true, localRegistrationEnabled: true };

  beforeEach(() => {
    show = vi.fn();

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: ToastService, useValue: { show } },
      ],
    });

    httpCtrl = TestBed.inject(HttpTestingController);
  });

  async function render(settings: Partial<AccountSettings> = {}): Promise<HTMLElement> {
    fixture = TestBed.createComponent(AccountSettingsScreen);
    fixture.detectChanges();
    httpCtrl.expectOne('/api/admin/settings').flush({ ...SETTINGS, ...settings });
    await settle();
    return fixture.nativeElement as HTMLElement;
  }

  /**
   * Lets pending promises run and re-renders. Deliberately not fixture.whenStable():
   * that waits for outstanding HTTP, and here there is always a request waiting to be
   * answered by the test itself, which deadlocks.
   */
  async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 0));
    fixture.detectChanges();
  }

  const switches = (el: HTMLElement) => [
    ...el.querySelectorAll<HTMLInputElement>('input[type="checkbox"]'),
  ];

  /** The nth switch, failing loudly rather than silently doing nothing if absent. */
  function toggle(el: HTMLElement, index: number): HTMLInputElement {
    const found = switches(el)[index];
    if (!found) throw new Error(`No switch at index ${index}; found ${switches(el).length}.`);
    return found;
  }

  it('degrades to its own message when the settings cannot be read', async () => {
    fixture = TestBed.createComponent(AccountSettingsScreen);
    fixture.detectChanges();
    httpCtrl.expectOne('/api/admin/settings').error(new ProgressEvent('network error'));
    await settle();

    // Reading value() on an errored resource throws. Unguarded, that throw happens
    // during change detection and takes the screen down before it can render the
    // branch written for exactly this case.
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('Could not load');
    expect(switches(el)).toHaveLength(0);
  });

  it('shows both settings together', async () => {
    const el = await render({ localLoginEnabled: true, localRegistrationEnabled: false });
    const signIn = toggle(el, 0);
    const registration = toggle(el, 1);

    // Shown together so the combination is visible rather than surprising.
    expect(signIn.checked).toBe(true);
    expect(registration.checked).toBe(false);
  });

  it('persists a change and re-reads what was stored', async () => {
    const el = await render();

    toggle(el, 1).click();
    await settle();

    const put = httpCtrl.expectOne((r) => r.method === 'PUT' && r.url === '/api/admin/settings');
    expect(put.request.body).toEqual({ localLoginEnabled: true, localRegistrationEnabled: false });
    put.flush({ localLoginEnabled: true, localRegistrationEnabled: false });
    await settle();

    // The stored value is the truth, so the screen re-reads rather than trusting the
    // switch it just moved.
    httpCtrl
      .expectOne('/api/admin/settings')
      .flush({ localLoginEnabled: true, localRegistrationEnabled: false });
    await settle();

    expect(toggle(fixture.nativeElement as HTMLElement, 1).checked).toBe(false);
    expect(show).toHaveBeenCalledWith('Account settings saved.', 'success');
  });

  it('keeps focus on the switch it was pressed on while the change saves', async () => {
    // A `disabled` attribute blurs the element it lands on, and that is the switch the
    // administrator just pressed.
    const el = await render();
    const registration = toggle(el, 1);

    registration.focus();
    registration.click();
    await settle();

    expect(document.activeElement).toBe(registration);
    expect(registration.disabled).toBe(false);
    expect(registration.getAttribute('aria-disabled')).toBe('true');

    httpCtrl
      .expectOne((r) => r.method === 'PUT')
      .flush({ localLoginEnabled: true, localRegistrationEnabled: true });
    await settle();
    httpCtrl
      .expectOne('/api/admin/settings')
      .flush({ localLoginEnabled: true, localRegistrationEnabled: true });
    await settle();
  });

  it('refuses a second toggle mid-save, and puts the switch it moved back', async () => {
    const el = await render();

    toggle(el, 1).click();
    await settle();

    // Still interactive, which is the point: a `disabled` attribute would have blurred
    // it. So the click lands, and `[checked]` is only rewritten when its bound value
    // changes — nothing has changed yet, so a refusal that left the box alone would
    // strand it out of step.
    expect(toggle(el, 0).disabled).toBe(false);
    toggle(el, 0).click();
    await settle();

    expect(toggle(el, 0).checked).toBe(true);

    httpCtrl
      .expectOne((r) => r.method === 'PUT')
      .flush({ localLoginEnabled: true, localRegistrationEnabled: true });
    await settle();
    httpCtrl
      .expectOne('/api/admin/settings')
      .flush({ localLoginEnabled: true, localRegistrationEnabled: true });
    await settle();
  });

  it('explains a refused sign-in change inline and leaves the switch on', async () => {
    const el = await render();

    toggle(el, 0).click();
    await settle();

    httpCtrl
      .expectOne((r) => r.method === 'PUT')
      .flush(
        {
          detail:
            'Sign in through the identity provider at least once with an administrator account first.',
        },
        { status: 409, statusText: 'Conflict' },
      );
    await settle();
    httpCtrl.expectOne('/api/admin/settings').flush(SETTINGS);
    await settle();

    const after = fixture.nativeElement as HTMLElement;
    // Inline, not a toast: the admin has to act on it, and they are reading while
    // deciding what to do next.
    expect(after.textContent).toContain('identity provider');
    expect(toggle(after, 0).checked).toBe(true);
    expect(show).not.toHaveBeenCalled();
  });

  it('falls back to a toast for a failure that is not the guard', async () => {
    const el = await render();

    toggle(el, 1).click();
    await settle();

    httpCtrl
      .expectOne((r) => r.method === 'PUT')
      .flush(null, { status: 500, statusText: 'Server Error' });
    await settle();
    httpCtrl.expectOne('/api/admin/settings').flush(SETTINGS);
    await settle();

    expect(show).toHaveBeenCalledWith('Could not save the account settings.', 'error');
  });
});

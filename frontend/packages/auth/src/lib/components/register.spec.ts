import { describe, expect, it, beforeEach, afterEach } from 'vitest';
import { provideZonelessChangeDetection } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import type { ClientConfig } from '@coffee-tracker/data';
import { Register } from './register';

/**
 * A fresh instance accepts exactly one registration and then closes, so this screen is
 * mostly a gate: what it must never do is show a form that cannot succeed. The rest is
 * the validation a submit has to survive before anything is sent.
 */
describe('Register', () => {
  let fixture: ComponentFixture<Register>;
  let http: HttpTestingController;

  const CONFIG: ClientConfig = {
    localLoginEnabled: true,
    registrationEnabled: true,
    oidcAvailable: false,
    oidc: null,
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    localStorage.clear();
    TestBed.resetTestingModule();
  });

  /** Renders against a given config and settles the resource read. */
  async function render(config: Partial<ClientConfig> = {}): Promise<HTMLElement> {
    fixture = TestBed.createComponent(Register);
    // detectChanges (not whenStable) to kick the resource: whenStable would wait on the
    // very request we are about to answer.
    fixture.detectChanges();
    http.expectOne('/api/config').flush({ ...CONFIG, ...config });
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const field = (el: HTMLElement, type: string): HTMLInputElement | null =>
    el.querySelector<HTMLInputElement>(`input[type="${type}"]`);

  function type(input: HTMLInputElement | null, value: string): void {
    if (!input) throw new Error('no such field');
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  async function submit(): Promise<void> {
    fixture.nativeElement.querySelector('form')?.dispatchEvent(new Event('submit'));
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('shows nothing to fill in while the config is still unknown', async () => {
    // registrationEnabled() is null until /api/config answers. Rendering the form on a
    // guess would flash a form that may be about to disappear.
    fixture = TestBed.createComponent(Register);
    fixture.detectChanges();

    expect(field(fixture.nativeElement, 'email')).toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Registration is closed');

    http.expectOne('/api/config').flush(CONFIG);
    await fixture.whenStable();
  });

  it('offers the form when the instance is still accepting an account', async () => {
    const el = await render();

    expect(field(el, 'email')).not.toBeNull();
    expect(field(el, 'password')).not.toBeNull();
    expect(el.querySelector('a[href="/login"]')).not.toBeNull();
  });

  it('closes the form once the instance has its account', async () => {
    const el = await render({ registrationEnabled: false });

    expect(field(el, 'email')).toBeNull();
    expect(el.textContent).toMatch(/closed/i);
    // The only way out has to stay reachable.
    expect(el.querySelector('a[href="/login"]')).not.toBeNull();
  });

  it('closes the form when the config cannot be read at all', async () => {
    // The opposite default to the sign-in screen's, and deliberately: a form that cannot
    // succeed is worse than a screen saying so, whereas a sign-in form might still work.
    fixture = TestBed.createComponent(Register);
    fixture.detectChanges();
    http.expectOne('/api/config').flush('nope', { status: 500, statusText: 'Error' });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(field(fixture.nativeElement, 'email')).toBeNull();
    expect((fixture.nativeElement as HTMLElement).textContent).toMatch(/closed/i);
  });

  it('sends nothing on an empty submit, and says why', async () => {
    const el = await render();

    await submit();

    // http.verify() would be ambiguous here; expectNone names the request that must not
    // have been made.
    http.expectNone({ method: 'POST', url: '/api/auth/register' });
    expect(el.querySelectorAll('.field-error').length).toBeGreaterThan(0);
  });

  it('refuses a password under eight characters before sending it', async () => {
    const el = await render();

    type(field(el, 'text'), 'Tester');
    type(field(el, 'email'), 'me@example.com');
    type(field(el, 'password'), 'short');
    await submit();

    http.expectNone({ method: 'POST', url: '/api/auth/register' });
    expect(el.textContent).toContain('At least 8 characters');
  });

  it('refuses an address that is not one', async () => {
    const el = await render();

    type(field(el, 'text'), 'Tester');
    type(field(el, 'email'), 'not-an-address');
    type(field(el, 'password'), 'longenough');
    await submit();

    http.expectNone({ method: 'POST', url: '/api/auth/register' });
    expect(el.textContent).toContain('Enter a valid email address');
  });

  it('registers a complete form', async () => {
    const el = await render();

    type(field(el, 'text'), 'Tester');
    type(field(el, 'email'), 'me@example.com');
    type(field(el, 'password'), 'longenough');
    await submit();

    const req = http.expectOne({ method: 'POST', url: '/api/auth/register' });
    expect(req.request.body).toEqual({
      email: 'me@example.com',
      password: 'longenough',
      displayName: 'Tester',
    });
  });
});

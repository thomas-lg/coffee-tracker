import { describe, expect, it, beforeEach, vi } from 'vitest';
import { provideZonelessChangeDetection, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import type { ClientConfig } from '@coffee-tracker/data';
import { Login } from './login';
import { ProviderSignIn } from '../provider-sign-in';

// What the sign-in screen offers is decided entirely by /api/config. Getting this
// wrong strands a visitor on a screen whose only visible way in does not work — or,
// worse, hides the one that does.
describe('Login', () => {
  let fixture: ComponentFixture<Login>;
  let httpCtrl: HttpTestingController;
  let start: ReturnType<typeof vi.fn>;

  const CONFIG: ClientConfig = {
    localLoginEnabled: true,
    registrationEnabled: true,
    oidcAvailable: false,
    oidc: null,
  };

  beforeEach(() => {
    start = vi.fn();

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: ProviderSignIn, useValue: { start, error: signal<string | null>(null) } },
      ],
    });

    httpCtrl = TestBed.inject(HttpTestingController);
  });

  /** Renders the screen against a given config and settles the resource read. */
  async function render(config: Partial<ClientConfig> = {}): Promise<HTMLElement> {
    fixture = TestBed.createComponent(Login);
    // detectChanges (not whenStable) to kick the resource: whenStable would wait on the
    // very request we are about to answer.
    fixture.detectChanges();
    httpCtrl.expectOne('/api/config').flush({ ...CONFIG, ...config });
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const hasPasswordField = (el: HTMLElement) => !!el.querySelector('input[type="password"]');
  const providerButton = (el: HTMLElement) =>
    [...el.querySelectorAll('ct-button')].find((b) => /identity provider/i.test(b.textContent ?? ''));
  const registerLink = (el: HTMLElement) => el.querySelector('a[href="/register"]');

  it('hides the provider action when no provider is configured', async () => {
    const el = await render({ oidcAvailable: false });

    expect(providerButton(el)).toBeUndefined();
    expect(hasPasswordField(el)).toBe(true);
  });

  it('offers the provider action when one is available', async () => {
    const el = await render({ oidcAvailable: true });

    // No name configured: the label stays generic rather than naming a product the
    // app has no business knowing about.
    expect(providerButton(el)).toBeDefined();
  });

  it('names the provider when the operator configured a name', async () => {
    const el = await render({
      oidcAvailable: true,
      oidc: { authority: 'https://id.example.com', clientId: 'c', scopes: 'openid', displayName: 'Authelia' },
    });

    const button = [...el.querySelectorAll('ct-button')].find((b) => /Authelia/.test(b.textContent ?? ''));
    expect(button).toBeDefined();
  });

  it('hides the local form when local sign-in is disabled', async () => {
    const el = await render({ localLoginEnabled: false, oidcAvailable: true });

    expect(hasPasswordField(el)).toBe(false);
    expect(providerButton(el)).toBeDefined();
  });

  it('hides the register link when registration is closed', async () => {
    const el = await render({ registrationEnabled: false });

    expect(registerLink(el)).toBeNull();
  });

  it('says so when the instance accepts no sign-in method at all', async () => {
    const el = await render({ localLoginEnabled: false, registrationEnabled: false, oidcAvailable: false });

    expect(hasPasswordField(el)).toBe(false);
    expect(el.textContent).toContain('no sign-in method');
  });

  it('keeps the local form when the config cannot be read', async () => {
    fixture = TestBed.createComponent(Login);
    fixture.detectChanges();
    httpCtrl.expectOne('/api/config').error(new ProgressEvent('network error'));
    await fixture.whenStable();
    fixture.detectChanges();

    // The form is the method that works with no server-side setup. Hiding it on a
    // transient error would leave a screen with no way in at all.
    expect(hasPasswordField(fixture.nativeElement as HTMLElement)).toBe(true);
  });

  it('starts the provider flow when the action is used', async () => {
    const el = await render({ oidcAvailable: true });

    (providerButton(el) as HTMLElement).querySelector('button')?.click();

    expect(start).toHaveBeenCalledOnce();
  });
});

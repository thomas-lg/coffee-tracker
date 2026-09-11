import { Injectable, inject, signal } from '@angular/core';
import {
  OidcSecurityService,
  provideAuth,
  StsConfigHttpLoader,
  StsConfigLoader,
  type LoginResponse,
} from 'angular-auth-oidc-client';
import { firstValueFrom, map } from 'rxjs';
import { ConfigApi } from '@coffee-tracker/data';
import { AuthStore } from './auth.store';

/**
 * Provider sign-in, wrapping the OIDC library so the rest of the app never sees it.
 *
 * The flow ends where the local one does: the provider's ID token is exchanged at the
 * API for the app's own session, so the interceptor, the refresh rotation and every
 * guard behave identically afterwards.
 */
@Injectable({ providedIn: 'root' })
export class ProviderSignIn {
  private readonly oidc = inject(OidcSecurityService);
  private readonly auth = inject(AuthStore);

  /** Set when a sign-in came back refused, so the login screen can say why. */
  readonly error = signal<string | null>(null);

  /** Sends the browser to the provider. Resolves only if the redirect never happens. */
  start(): void {
    this.error.set(null);
    this.oidc.authorize();
  }

  /**
   * Completes a sign-in the provider redirected back from. Returns true when a session
   * was established, false when there was nothing to complete (an ordinary page load).
   */
  async complete(): Promise<boolean> {
    let result: LoginResponse;
    try {
      result = await firstValueFrom(this.oidc.checkAuth());
    } catch {
      // This runs during bootstrap: a provider that is unreachable, or a malformed
      // callback, must not stop the app from starting. Local sign-in still works.
      return false;
    }

    if (!result.isAuthenticated || !result.idToken) {
      return false;
    }

    // Drop ?code=… before anything can reload with it: the API spends a token once, so
    // a refresh carrying a used code would surface as a failed sign-in.
    history.replaceState(null, '', window.location.pathname);

    try {
      await this.auth.signInWithProviderToken(result.idToken);
      return true;
    } catch (err: unknown) {
      // The API refused the token — a conflicting unverified email, most likely. Its
      // explanation is the useful one; the library's state is not.
      this.error.set(messageFor(err));
      // Drop the library's session too: keeping it would make the next page load
      // retry an exchange that just failed.
      this.oidc.logoffLocal();
      return false;
    }
  }
}

function messageFor(err: unknown): string {
  const detail = (err as { error?: { detail?: string } })?.error?.detail;
  return detail ?? 'Signing in with the identity provider failed.';
}

/**
 * Configures the library from `/api/config` at startup, so the provider is set in the
 * container alone and the built app carries no deployment-specific configuration.
 * An instance with no provider yields a config the library simply never uses.
 */
export function provideProviderSignIn() {
  return provideAuth({
    loader: {
      provide: StsConfigLoader,
      useFactory: () => {
        const config = inject(ConfigApi);
        return new StsConfigHttpLoader(
          config.get().pipe(
            map((c) => ({
              authority: c.oidc?.authority ?? '',
              redirectUrl: `${window.location.origin}/`,
              postLogoutRedirectUri: window.location.origin,
              clientId: c.oidc?.clientId ?? '',
              scope: c.oidc?.scopes ?? 'openid profile email',
              responseType: 'code',
              // The app's own token is what authorizes requests; the library only ever
              // has to get us an ID token once.
              silentRenew: false,
              useRefreshToken: false,
              autoUserInfo: false,
            })),
          ),
        );
      },
    },
  });
}

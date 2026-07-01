import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { InjectionToken, inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SKIP_AUTH_REDIRECT } from '@coffee-tracker/data';
import { AuthStore } from './auth.store';

/**
 * The "reload the page" action, injected so tests can substitute a spy —
 * `document.location.reload()` isn't implemented in jsdom and can't be spied on.
 */
export const RELOAD = new InjectionToken<() => void>('RELOAD', {
  factory: () => () => document.location.reload(),
});

/**
 * Minimum gap between gateway-triggered reloads. If the gateway keeps answering
 * 401 after a reload (misconfiguration rather than an expired session), further
 * failures surface as regular errors instead of a reload loop.
 */
const GATEWAY_RELOAD_MIN_MS = 60_000;
const GATEWAY_RELOAD_AT = 'ct.gatewayReloadAt';

/** Attaches the bearer token to /api requests and bounces to /login on a 401. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  const reload = inject(RELOAD);

  const token = auth.token();
  const request =
    token && req.url.startsWith('/api')
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(request).pipe(
    catchError((err: HttpErrorResponse) => {
      // A 401 on a real (already-authenticated) call means a session died — but
      // whose? The API's JwtBearer challenge always carries `WWW-Authenticate:
      // Bearer`; a 401 minted by an auth gateway in front of the app (Authelia,
      // Cloudflare Access, …) doesn't. Requests to anonymous endpoints opt out
      // via SKIP_AUTH_REDIRECT so their 401s (e.g. "invalid credentials") reach
      // the caller.
      if (err.status === 401 && !req.context.get(SKIP_AUTH_REDIRECT)) {
        if (err.headers.get('WWW-Authenticate')?.includes('Bearer')) {
          // Our own token died — clear it and return to login.
          auth.logout();
          void router.navigateByUrl('/login');
        } else {
          // The gateway session expired while our token is still good. A fetch
          // can't follow the gateway's login redirect, but a full document
          // request can — reload so the gateway re-authenticates the browser
          // and we land back here still signed in.
          reloadForGateway(reload);
        }
      }
      return throwError(() => err);
    }),
  );
};

function reloadForGateway(reload: () => void): void {
  const last = Number(sessionStorage.getItem(GATEWAY_RELOAD_AT)) || 0;
  if (Date.now() - last < GATEWAY_RELOAD_MIN_MS) return;
  sessionStorage.setItem(GATEWAY_RELOAD_AT, String(Date.now()));
  reload();
}

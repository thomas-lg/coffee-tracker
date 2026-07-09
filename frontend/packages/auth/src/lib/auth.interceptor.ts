import {
  HttpErrorResponse,
  HttpEvent,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, from, switchMap, throwError } from 'rxjs';
import { SKIP_AUTH_REDIRECT } from '@coffee-tracker/data';
import { RELOAD } from '@coffee-tracker/util';
import { AuthStore } from './auth.store';

/**
 * Minimum gap between gateway-triggered reloads. If the gateway keeps answering
 * 401 after a reload (misconfiguration rather than an expired session), further
 * failures surface as regular errors instead of a reload loop.
 */
const GATEWAY_RELOAD_MIN_MS = 60_000;
const GATEWAY_RELOAD_AT = 'ct.gatewayReloadAt';

/**
 * Attaches the bearer token to /api requests and recovers from 401s:
 *
 * - An API 401 (JwtBearer challenge) usually just means the short-lived access token
 *   expired — exchange the refresh token for a new pair and retry the request ONCE.
 *   Only if that fails (refresh token dead, or the retry still 401s) do we clear the
 *   session and return to /login.
 * - A gateway 401 (no Bearer challenge — Authelia & co in front of the app) is handled
 *   by a throttled full-page reload so the gateway can re-authenticate the browser.
 * - Requests to anonymous endpoints (login/refresh/config, …) opt out via
 *   SKIP_AUTH_REDIRECT so their 401s reach the caller — this also keeps the refresh
 *   call itself from ever re-entering the refresh logic.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  const reload = inject(RELOAD);

  return next(withToken(req, auth.token())).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401 || req.context.get(SKIP_AUTH_REDIRECT)) {
        return throwError(() => err);
      }

      // A 401 minted by an auth gateway in front of the app carries no JwtBearer
      // challenge. A fetch can't follow the gateway's login redirect, but a full
      // document request can — reload so the gateway re-authenticates the browser.
      if (!isBearerChallenge(err)) {
        reloadForGateway(reload);
        return throwError(() => err);
      }

      // Our own access token died. Refresh + retry once when we can.
      if (auth.canRefresh()) {
        return from(auth.refresh()).pipe(
          switchMap((refreshed): Observable<HttpEvent<unknown>> => {
            if (!refreshed) {
              // refresh() already cleared the dead session.
              void router.navigateByUrl('/login');
              return throwError(() => err);
            }
            return next(withToken(req, auth.token())).pipe(
              catchError((retryErr: HttpErrorResponse) => {
                // A fresh token that still 401s isn't recoverable — give up cleanly
                // instead of looping (next(...) here bypasses this interceptor, so
                // one retry is structurally guaranteed).
                if (retryErr.status === 401 && isBearerChallenge(retryErr)) {
                  auth.logout();
                  void router.navigateByUrl('/login');
                }
                return throwError(() => retryErr);
              }),
            );
          }),
        );
      }

      // No refresh token to fall back on — clear and return to login.
      auth.logout();
      void router.navigateByUrl('/login');
      return throwError(() => err);
    }),
  );
};

function withToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token && req.url.startsWith('/api')
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;
}

/** The API's JwtBearer challenge always carries `WWW-Authenticate: Bearer`. */
function isBearerChallenge(err: HttpErrorResponse): boolean {
  return !!err.headers.get('WWW-Authenticate')?.includes('Bearer');
}

function reloadForGateway(reload: () => void): void {
  const last = Number(sessionStorage.getItem(GATEWAY_RELOAD_AT)) || 0;
  if (Date.now() - last < GATEWAY_RELOAD_MIN_MS) return;
  sessionStorage.setItem(GATEWAY_RELOAD_AT, String(Date.now()));
  reload();
}

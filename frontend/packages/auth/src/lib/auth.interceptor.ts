import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { SKIP_AUTH_REDIRECT } from '@coffee-tracker/data';
import { AuthStore } from './auth.store';

/** Attaches the bearer token to /api requests and bounces to /login on a 401. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStore);
  const router = inject(Router);

  const token = auth.token();
  const request =
    token && req.url.startsWith('/api')
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

  return next(request).pipe(
    catchError((err: HttpErrorResponse) => {
      // A 401 on a real (already-authenticated) call means the token died — clear it
      // and return to login. Requests to anonymous endpoints opt out via
      // SKIP_AUTH_REDIRECT so their 401s (e.g. "invalid credentials") reach the caller.
      if (err.status === 401 && !req.context.get(SKIP_AUTH_REDIRECT)) {
        auth.logout();
        void router.navigateByUrl('/login');
      }
      return throwError(() => err);
    }),
  );
};

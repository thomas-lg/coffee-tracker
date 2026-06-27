import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
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
      // and return to login. Don't hijack the login/register/config endpoints, whose
      // 401s are the caller's to surface (e.g. "invalid credentials").
      const isAuthEndpoint = req.url.startsWith('/api/auth') || req.url.startsWith('/api/config');
      if (err.status === 401 && !isAuthEndpoint) {
        auth.logout();
        void router.navigateByUrl('/login');
      }
      return throwError(() => err);
    }),
  );
};

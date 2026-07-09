import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from './auth.store';

/**
 * Protects routes: unauthenticated users are redirected to /login. When only the
 * short-lived access token has expired, the stored refresh token is exchanged for a
 * new pair instead of bouncing the user out.
 */
export const authGuard: CanActivateFn = async () => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  if (auth.hasValidAccessToken()) return true;
  if (auth.canRefresh() && (await auth.refresh())) return true;
  return router.createUrlTree(['/login']);
};

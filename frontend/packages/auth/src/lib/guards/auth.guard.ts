import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '@auth/services/auth.store';

/**
 * Protects routes: unauthenticated users are redirected to /login. When only the
 * short-lived access token has expired, the stored refresh token is exchanged for a
 * new pair instead of bouncing the user out.
 *
 * The attempted URL rides along as `returnUrl` so sign-in can resume it. This is the
 * common path, not an edge case: the app is a PWA that lives in a tab for days, so a
 * deep link opened against an expired, unrefreshable session is how people usually
 * arrive at the login screen.
 */
export const authGuard: CanActivateFn = async (_route, state) => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  if (auth.hasValidAccessToken()) return true;
  if (auth.canRefresh() && (await auth.refresh())) return true;
  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

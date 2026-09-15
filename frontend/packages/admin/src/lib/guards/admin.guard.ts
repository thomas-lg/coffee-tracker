import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from '@coffee-tracker/auth';

/** Admin-only routes: non-admins (and anonymous) are bounced to the home page. */
export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  return auth.isAdmin() ? true : router.createUrlTree(['/']);
};

import { HttpContextToken } from '@angular/common/http';

/**
 * Marks a request to an anonymous endpoint (login/register/config). The auth
 * interceptor surfaces a 401 on these to the caller instead of triggering the
 * global logout-and-redirect — set by the caller, not inferred from the URL.
 */
export const SKIP_AUTH_REDIRECT = new HttpContextToken<boolean>(() => false);

import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
  isDevMode,
} from '@angular/core';
import {
  PreloadAllModules,
  provideRouter,
  withComponentInputBinding,
  withPreloading,
  withViewTransitions,
} from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { authInterceptor, ProviderSignIn, provideProviderSignIn } from '@coffee-tracker/auth';

import { routes } from './app.routes';
import { provideServiceWorker } from '@angular/service-worker';
import { provideAppUpdates } from './services/app-updates';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // State-of-the-art Angular: no Zone.js. Change detection is signal-driven.
    provideZonelessChangeDetection(),
    provideRouter(
      routes,
      withComponentInputBinding(),
      withViewTransitions(),
      // Preload lazy feature chunks after first paint so nav (e.g. Browse) is instant.
      withPreloading(PreloadAllModules),
    ),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    provideProviderSignIn(),
    // The provider redirects back to the app root carrying ?code=…, and the exchange
    // has to finish BEFORE the router runs: otherwise the auth guard sees no session,
    // redirects to /login, and the code goes with the URL, the sign-in silently ends
    // back on the login screen with nothing to show for it.
    provideAppInitializer(() => inject(ProviderSignIn).complete()),
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      registrationStrategy: 'registerWhenStable:30000',
    }),
    // Reload onto a freshly deployed version instead of serving the cached one.
    provideAppUpdates(),
  ],
};

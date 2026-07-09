import { InjectionToken } from '@angular/core';

/**
 * The "reload the page" action, injected so tests can substitute a spy —
 * `document.location.reload()` isn't implemented in jsdom and can't be spied on.
 * Shared by the auth interceptor (gateway re-auth) and the SW update handler.
 */
export const RELOAD = new InjectionToken<() => void>('RELOAD', {
  factory: () => () => document.location.reload(),
});

import {
  EnvironmentProviders,
  InjectionToken,
  inject,
  provideAppInitializer,
} from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { SwUpdate } from '@angular/service-worker';
import { filter, take } from 'rxjs/operators';

/**
 * How often to poll for a freshly deployed version while the app stays open.
 * The service worker also checks on navigation, but a PWA tab can sit open for
 * days — without a poll, a NAS container redeploy wouldn't be noticed until the
 * user navigates. Six hours is frequent enough to pick up a deploy by the next
 * session without hammering the server.
 */
const UPDATE_POLL_MS = 6 * 60 * 60 * 1000;

/**
 * The "reload the page" action, injected so tests can substitute a spy —
 * `document.location.reload()` isn't implemented in jsdom and can't be spied on.
 */
export const RELOAD = new InjectionToken<() => void>('RELOAD', {
  factory: () => () => document.location.reload(),
});

/**
 * Wire service-worker update handling. Extracted from the provider so it can be
 * unit-tested with fakes — see app-updates.spec.ts.
 *
 * When a new version is ready we don't reload on the spot: that would wipe an
 * in-progress form (the coffee form holds an unsaved model, a chosen photo, and
 * OCR-prefilled fields). Instead we wait for the next route navigation, which
 * already abandons the current view, and reload then. An unrecoverable SW state
 * has nothing worth preserving, so that one reloads immediately.
 */
export function setupAppUpdates(updates: SwUpdate, router: Router, reload: () => void): void {
  if (!updates.isEnabled) return;

  updates.versionUpdates
    .pipe(
      filter((evt) => evt.type === 'VERSION_READY'),
      take(1),
    )
    .subscribe(() => {
      // Reload on the next navigation so we never interrupt the current view
      // mid-edit. If a navigation is already in flight, NavigationEnd still
      // fires for it and we reload onto its target.
      router.events
        .pipe(
          filter((evt) => evt instanceof NavigationEnd),
          take(1),
        )
        .subscribe(() => reload());
    });

  // A broken cache state can't render the app at all — reload now to re-fetch
  // everything from the (updated) server rather than the dead cache.
  updates.unrecoverable.subscribe(() => reload());

  // Kick an immediate check, then poll. checkForUpdate() rejects if the SW is
  // disabled mid-flight; swallow it so the initializer never blocks startup.
  const check = () => void updates.checkForUpdate().catch(() => {});
  check();
  setInterval(check, UPDATE_POLL_MS);
}

/**
 * Auto-activate new app versions instead of stranding users on a cached one.
 *
 * Without this, the Angular service worker downloads a new build in the
 * background but keeps serving the OLD version to the open page until every tab
 * is closed — a plain refresh won't swap it. That's why a redeploy can look like
 * "I'm still on the old version".
 */
export function provideAppUpdates(): EnvironmentProviders {
  return provideAppInitializer(() => setupAppUpdates(inject(SwUpdate), inject(Router), inject(RELOAD)));
}

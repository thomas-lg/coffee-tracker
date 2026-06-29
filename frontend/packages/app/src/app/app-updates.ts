import { EnvironmentProviders, inject, provideAppInitializer } from '@angular/core';
import { SwUpdate } from '@angular/service-worker';
import { filter } from 'rxjs/operators';

/**
 * How often to poll for a freshly deployed version while the app stays open.
 * The service worker also checks on navigation, but a PWA tab can sit open for
 * days — without a poll, a NAS container redeploy wouldn't be noticed until the
 * user navigates. Six hours is frequent enough to pick up a deploy by the next
 * session without hammering the server.
 */
const UPDATE_POLL_MS = 6 * 60 * 60 * 1000;

/**
 * Auto-activate new app versions instead of stranding users on a cached one.
 *
 * Without this, the Angular service worker downloads a new build in the
 * background but keeps serving the OLD version to the open page until every tab
 * is closed — a plain refresh won't swap it. That's why a redeploy can look like
 * "I'm still on the old version". Here we reload as soon as the new version is
 * ready (VERSION_READY), and also recover from a broken cache state.
 */
export function provideAppUpdates(): EnvironmentProviders {
  return provideAppInitializer(() => {
    const updates = inject(SwUpdate);
    if (!updates.isEnabled) return;

    updates.versionUpdates
      .pipe(filter((evt) => evt.type === 'VERSION_READY'))
      .subscribe(() => document.location.reload());

    // If the SW lands in an unrecoverable state, a hard reload re-fetches
    // everything from the (updated) server rather than the dead cache.
    updates.unrecoverable.subscribe(() => document.location.reload());

    // Kick an immediate check, then poll. checkForUpdate() rejects if the SW is
    // disabled mid-flight; swallow it so the initializer never blocks startup.
    const check = () => void updates.checkForUpdate().catch(() => {});
    check();
    setInterval(check, UPDATE_POLL_MS);
  });
}

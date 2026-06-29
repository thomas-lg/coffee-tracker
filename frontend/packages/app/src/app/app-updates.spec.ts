import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { Subject } from 'rxjs';
import { NavigationEnd, type Event as RouterEvent, type Router } from '@angular/router';
import type { SwUpdate, VersionEvent, UnrecoverableStateEvent } from '@angular/service-worker';
import { setupAppUpdates } from './app-updates';

/** Minimal fakes: only the members setupAppUpdates touches, backed by Subjects we drive. */
function harness(isEnabled = true) {
  const versionUpdates = new Subject<VersionEvent>();
  const unrecoverable = new Subject<UnrecoverableStateEvent>();
  const events = new Subject<RouterEvent>();
  const checkForUpdate = vi.fn(() => Promise.resolve(false));
  const reload = vi.fn();

  const updates = { isEnabled, versionUpdates, unrecoverable, checkForUpdate } as unknown as SwUpdate;
  const router = { events } as unknown as Router;

  setupAppUpdates(updates, router, reload);
  return { versionUpdates, unrecoverable, events, checkForUpdate, reload };
}

const versionReady = { type: 'VERSION_READY' } as VersionEvent;
const navEnd = () => new NavigationEnd(1, '/coffees/1', '/coffees/1');

describe('setupAppUpdates', () => {
  beforeEach(() => vi.useFakeTimers());
  afterEach(() => vi.useRealTimers());

  it('does nothing when the service worker is disabled (dev mode)', () => {
    const h = harness(false);
    h.versionUpdates.next(versionReady);
    h.events.next(navEnd());
    h.unrecoverable.next({ type: 'UNRECOVERABLE_STATE', reason: 'x' } as UnrecoverableStateEvent);
    expect(h.reload).not.toHaveBeenCalled();
    expect(h.checkForUpdate).not.toHaveBeenCalled();
  });

  it('does not reload immediately when a new version is ready', () => {
    const h = harness();
    h.versionUpdates.next(versionReady);
    expect(h.reload).not.toHaveBeenCalled();
  });

  it('reloads on the next navigation after a new version is ready', () => {
    const h = harness();
    h.versionUpdates.next(versionReady);
    h.events.next(navEnd());
    expect(h.reload).toHaveBeenCalledTimes(1);
  });

  it('does not reload on navigation when no new version is pending', () => {
    const h = harness();
    h.events.next(navEnd());
    expect(h.reload).not.toHaveBeenCalled();
  });

  it('reloads at most once even across several navigations', () => {
    const h = harness();
    h.versionUpdates.next(versionReady);
    h.events.next(navEnd());
    h.events.next(navEnd());
    expect(h.reload).toHaveBeenCalledTimes(1);
  });

  it('reloads immediately on an unrecoverable service-worker state', () => {
    const h = harness();
    h.unrecoverable.next({ type: 'UNRECOVERABLE_STATE', reason: 'cache gone' } as UnrecoverableStateEvent);
    expect(h.reload).toHaveBeenCalledTimes(1);
  });
});

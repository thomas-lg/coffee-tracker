import {
  DestroyRef,
  Injector,
  Signal,
  assertInInjectionContext,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { type MonoTypeOperatorFunction, Observable, tap } from 'rxjs';
import type { RequestStatus } from './request-status.feature';

/** One command from the outside: not started, in flight, or settled one way or another. */
export type ActionState = 'idle' | 'running' | 'done' | 'failed';

/** What a caller is likely to already have in hand. See `toActionState`. */
export type ActionStateLike = ActionState | RequestStatus | boolean | null | undefined;

/**
 * For use as an input transform, so a component works in one vocabulary whatever it is
 * handed. Both shapes are legitimate: a store with one command exposes the whole
 * `requestStatus()`, one with several keeps a boolean each, and a boolean can only ever
 * say running or not.
 */
export function toActionState(value: ActionStateLike): ActionState {
  if (value == null) return 'idle';
  if (typeof value === 'boolean') return value ? 'running' : 'idle';
  if (typeof value === 'object') return 'failed';
  switch (value) {
    case 'pending':
    case 'running':
      return 'running';
    case 'fulfilled':
    case 'done':
      return 'done';
    case 'failed':
      return 'failed';
    default:
      return 'idle';
  }
}

/**
 * Runs `action` when `state` stops running, with the outcome it settled on (`idle` when
 * fed from a boolean, which cannot report one).
 *
 * The edge, not the level: "not running" is also true before anything started and forever
 * after, so reacting to the value alone fires on load instead of on the event.
 */
export function onActionSettled(
  state: Signal<ActionState>,
  action: (outcome: Exclude<ActionState, 'running'>) => void,
  options?: { injector?: Injector },
): void {
  if (!options?.injector) assertInInjectionContext(onActionSettled);

  let previous = untracked(state);
  effect(() => {
    const current = state();
    const settled = previous === 'running' && current !== 'running';
    previous = current;
    if (settled) action(current);
  }, options);
}

/** Long enough to read as a confirmation, over before the user reaches for the next thing. */
export const ACTION_OUTCOME_MS = 1000;

export type ActionTracker = {
  readonly state: Signal<ActionState>;
  readonly running: Signal<boolean>;
  readonly failed: Signal<boolean>;
  start(): void;
  succeed(): void;
  fail(): void;
  reset(): void;
};

/**
 * Tracks one command, as an object a store can hold several of, named `<verb>Action` to
 * leave the bare verb to the method that does the work.
 *
 * Deliberately not a store feature the way `withRequestStatus` is: a feature applies once
 * per store, and says so itself ("one command in flight per store"), while these screens
 * run two commands each.
 *
 * An outcome is transient, which lets a control confirm itself with nothing to schedule,
 * and is also the line between the two: `withRequestStatus` carries the error *text*, and
 * a message the user has not read yet must not be taken away by a timer.
 */
export function createActionState(options?: { injector?: Injector }): ActionTracker {
  if (!options?.injector) assertInInjectionContext(createActionState);

  const state = signal<ActionState>('idle');
  let returnToRest: ReturnType<typeof setTimeout> | undefined;

  const cancelReturn = (): void => {
    clearTimeout(returnToRest);
    returnToRest = undefined;
  };

  /** Cancels a pending return, so a new run is never cut short by the last one's fade. */
  const moveTo = (next: ActionState): void => {
    cancelReturn();
    state.set(next);
  };

  const settleOn = (outcome: 'done' | 'failed'): void => {
    moveTo(outcome);
    returnToRest = setTimeout(() => state.set('idle'), ACTION_OUTCOME_MS);
  };

  // A pending return would otherwise outlive everyone who was reading it.
  const destroyRef = options?.injector?.get(DestroyRef) ?? inject(DestroyRef);
  destroyRef.onDestroy(cancelReturn);

  return {
    state: state.asReadonly(),
    running: computed(() => state() === 'running'),
    failed: computed(() => state() === 'failed'),
    start: () => moveTo('running'),
    succeed: () => settleOn('done'),
    fail: () => settleOn('failed'),
    reset: () => moveTo('idle'),
  };
}

/**
 * Reports the command a stream stands for: running from the moment it is subscribed,
 * then the outcome it terminates on.
 *
 * Belongs inside the flattening operator, not before it, so a click `exhaustMap` is
 * about to ignore does not restart the state of the run already in flight.
 *
 * The `settled` flag is what tells a teardown apart from a terminated stream: unsubscribed
 * mid-flight, the command did not fail, it simply stopped being asked for.
 */
export function trackAction<T>(action: ActionTracker): MonoTypeOperatorFunction<T> {
  return (source$) =>
    new Observable<T>((subscriber) => {
      action.start();
      let settled = false;

      return source$
        .pipe(
          tap({
            next: () => {
              settled = true;
              action.succeed();
            },
            error: () => {
              settled = true;
              action.fail();
            },
            finalize: () => {
              if (!settled) action.reset();
            },
          }),
        )
        .subscribe(subscriber);
    });
}

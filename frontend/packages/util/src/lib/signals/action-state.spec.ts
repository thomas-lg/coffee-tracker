import { afterEach, describe, expect, it, vi } from 'vitest';
import { Injector, provideZonelessChangeDetection, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject, exhaustMap, of } from 'rxjs';
import {
  ACTION_OUTCOME_MS,
  type ActionState,
  createActionState,
  onActionSettled,
  toActionState,
  trackAction,
} from './action-state';

describe('toActionState', () => {
  it('reads a boolean as running or not started', () => {
    expect(toActionState(true)).toBe('running');
    expect(toActionState(false)).toBe('idle');
  });

  it('reads a request status, outcome included', () => {
    expect(toActionState('idle')).toBe('idle');
    expect(toActionState('pending')).toBe('running');
    expect(toActionState('fulfilled')).toBe('done');
    expect(toActionState({ error: 'boom' })).toBe('failed');
  });

  it('passes an ActionState through, and treats nothing as not started', () => {
    expect(toActionState('failed')).toBe('failed');
    expect(toActionState(null)).toBe('idle');
    expect(toActionState(undefined)).toBe('idle');
  });
});

describe('onActionSettled', () => {
  function watch(initial: ActionState) {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
    const injector = TestBed.inject(Injector);
    const state = signal<ActionState>(initial);
    const outcomes: string[] = [];
    onActionSettled(state, (outcome) => outcomes.push(outcome), { injector });
    TestBed.tick();
    return { state, outcomes };
  }

  it('says nothing until a run has actually finished', () => {
    const { state, outcomes } = watch('idle');
    expect(outcomes).toEqual([]);

    state.set('running');
    TestBed.tick();
    expect(outcomes).toEqual([]);

    state.set('done');
    TestBed.tick();
    expect(outcomes).toEqual(['done']);
  });

  it('reports the outcome it settled on', () => {
    const { state, outcomes } = watch('running');

    state.set('failed');
    TestBed.tick();

    expect(outcomes).toEqual(['failed']);
  });

  it('says nothing more when the outcome fades back to rest', () => {
    const { state, outcomes } = watch('running');

    state.set('done');
    TestBed.tick();
    state.set('idle');
    TestBed.tick();

    expect(outcomes).toEqual(['done']);
  });

  it('settles a run that starts and finishes inside one turn', () => {
    // Signal writes are coalesced, so the effect only ever sees the outcome. Waiting for
    // a `running` that no pass rendered would drop the event entirely.
    const { state, outcomes } = watch('idle');

    state.set('running');
    state.set('done');
    TestBed.tick();

    expect(outcomes).toEqual(['done']);
  });

  it('ignores a state that was already holding an outcome when it began', () => {
    // What a store-wide status looks like to the next screen that reads it: settled by
    // someone else's command, and nothing to report here.
    const { state, outcomes } = watch('done');

    state.set('idle');
    TestBed.tick();

    expect(outcomes).toEqual([]);
  });
});

describe('createActionState', () => {
  afterEach(() => {
    vi.useRealTimers();
    TestBed.resetTestingModule();
  });

  function make() {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
    return TestBed.runInInjectionContext(() => createActionState());
  }

  it('starts idle and reports each outcome', () => {
    vi.useFakeTimers();
    const action = make();
    expect(action.state()).toBe('idle');

    action.start();
    expect(action.state()).toBe('running');
    expect(action.running()).toBe(true);

    action.succeed();
    expect(action.state()).toBe('done');
    expect(action.running()).toBe(false);

    action.start();
    action.fail();
    expect(action.state()).toBe('failed');
    expect(action.failed()).toBe(true);

    action.reset();
    expect(action.state()).toBe('idle');
  });

  it('holds an outcome long enough to be read, then returns to rest by itself', () => {
    vi.useFakeTimers();
    const action = make();

    action.start();
    action.succeed();
    vi.advanceTimersByTime(ACTION_OUTCOME_MS - 1);
    expect(action.state()).toBe('done');

    vi.advanceTimersByTime(1);
    expect(action.state()).toBe('idle');
  });

  it('lets a new run cancel the previous outcome, rather than being cut short by it', () => {
    vi.useFakeTimers();
    const action = make();

    action.succeed();
    vi.advanceTimersByTime(ACTION_OUTCOME_MS / 2);
    action.start();
    // The first run's return would land in here if it had not been cancelled.
    vi.advanceTimersByTime(ACTION_OUTCOME_MS);

    expect(action.state()).toBe('running');
  });

  it('settles once per run, not again when the outcome fades', () => {
    vi.useFakeTimers();
    const injector =
      (TestBed.configureTestingModule({
        providers: [provideZonelessChangeDetection()],
      }),
      TestBed.inject(Injector));
    const action = TestBed.runInInjectionContext(() => createActionState());
    const outcomes: string[] = [];
    onActionSettled(action.state, (outcome) => outcomes.push(outcome), { injector });
    TestBed.tick();

    action.start();
    TestBed.tick();
    action.succeed();
    TestBed.tick();
    vi.advanceTimersByTime(ACTION_OUTCOME_MS);
    TestBed.tick();

    expect(outcomes).toEqual(['done']);
  });

  it('gives each command its own state, which is the whole point', () => {
    const rate = make();
    const remove = TestBed.runInInjectionContext(() => createActionState());

    rate.start();

    expect(rate.running()).toBe(true);
    expect(remove.running()).toBe(false);
  });
});

describe('trackAction', () => {
  afterEach(() => TestBed.resetTestingModule());

  function tracker() {
    TestBed.configureTestingModule({ providers: [provideZonelessChangeDetection()] });
    return TestBed.runInInjectionContext(() => createActionState());
  }

  it('runs from the subscription and reports what the stream terminated on', () => {
    const action = tracker();
    const request = new Subject<string>();
    const tracked = request.pipe(trackAction(action));

    // Cold until subscribed: the state belongs to a run, not to the pipe being built.
    expect(action.state()).toBe('idle');

    const sub = tracked.subscribe({ error: () => undefined });
    expect(action.state()).toBe('running');

    request.next('ok');
    expect(action.state()).toBe('done');
    sub.unsubscribe();
  });

  it('reports a failure, and does not swallow it', () => {
    const action = tracker();
    const request = new Subject<string>();
    let seen: unknown;

    request.pipe(trackAction(action)).subscribe({
      error: (err: unknown) => {
        seen = err;
      },
    });
    request.error(new Error('nope'));

    expect(action.state()).toBe('failed');
    expect(seen).toBeInstanceOf(Error);
  });

  it('returns to rest when dropped mid-flight, which is not a failure', () => {
    const action = tracker();
    const request = new Subject<string>();

    const sub = request.pipe(trackAction(action)).subscribe();
    expect(action.state()).toBe('running');
    sub.unsubscribe();

    expect(action.state()).toBe('idle');
  });

  it('does not restart the run in flight for a click exhaustMap ignores', () => {
    const action = tracker();
    const trigger = new Subject<void>();
    const request = new Subject<string>();
    const starts: ActionState[] = [];

    trigger
      .pipe(exhaustMap(() => request.pipe(trackAction(action))))
      .subscribe(() => starts.push(action.state()));

    trigger.next();
    trigger.next(); // ignored: the first is still in flight
    request.next('ok');

    expect(starts).toEqual(['done']);
    expect(action.state()).toBe('done');
  });

  it('keeps the outcome when a synchronous stream completes right behind its value', () => {
    const action = tracker();
    of(1).pipe(trackAction(action)).subscribe();

    // The completion must not be read as a teardown and reset what the value settled.
    expect(action.state()).toBe('done');
  });
});

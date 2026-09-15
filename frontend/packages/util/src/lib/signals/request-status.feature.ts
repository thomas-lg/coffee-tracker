import { computed } from '@angular/core';
import {
  patchState,
  signalStoreFeature,
  withComputed,
  withMethods,
  withState,
} from '@ngrx/signals';

/** idle → pending → fulfilled | { error }. One command in flight per store. */
export type RequestStatus = 'idle' | 'pending' | 'fulfilled' | { readonly error: string };

export type RequestStatusState = { requestStatus: RequestStatus };

/**
 * The members are `pending`/`requestError`, not `loading`/`error`: the stores that read
 * through a resource already expose `loading` and `error` for the *read*, and a
 * SignalStore rejects colliding member names at the type level. Keeping them distinct is
 * also what lets a screen tell "the list failed to load" apart from "my delete failed".
 */
export function withRequestStatus() {
  return signalStoreFeature(
    withState<RequestStatusState>({ requestStatus: 'idle' }),
    withComputed(({ requestStatus }) => ({
      pending: computed(() => requestStatus() === 'pending'),
      fulfilled: computed(() => requestStatus() === 'fulfilled'),
      requestError: computed(() => {
        const status = requestStatus();
        return typeof status === 'object' ? status.error : null;
      }),
    })),
    withMethods((store) => ({
      /**
       * Root-provided stores outlive the screen that used them, so an error left by one
       * screen would render on the next. Call this when a screen takes over.
       */
      resetRequestStatus(): void {
        patchState(store, { requestStatus: 'idle' });
      },
    })),
  );
}

export const setPending = (): RequestStatusState => ({ requestStatus: 'pending' });

export const setFulfilled = (): RequestStatusState => ({ requestStatus: 'fulfilled' });

export const setRequestError = (error: string): RequestStatusState => ({
  requestStatus: { error },
});

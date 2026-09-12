import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { FormField, FormRoot, email, form, required } from '@angular/forms/signals';
import { Button, Icon, ToastService } from '@coffee-tracker/ui';
import { ConfigApi } from '@coffee-tracker/data';
import { AuthStore } from '../auth.store';
import { ProviderSignIn } from '../provider-sign-in';

@Component({
  selector: 'ct-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, FormRoot, RouterLink, Button, Icon],
  templateUrl: './login.html',
})
export class Login {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly config = inject(ConfigApi);
  private readonly provider = inject(ProviderSignIn);

  private readonly configRes = rxResource({ stream: () => this.config.get() });

  /**
   * null while the config is still loading, so the screen shows neither method rather
   * than flashing one that may not be offered.
   *
   * A failed config load leaves local sign-in on: the form is the method that works
   * without any server-side setup, and hiding it on a transient error would leave a
   * screen with no way in at all.
   */
  protected readonly localLoginEnabled = computed<boolean | null>(() => {
    if (this.configRes.error()) return true;
    return this.configRes.value()?.localLoginEnabled ?? null;
  });

  protected readonly registrationEnabled = computed<boolean | null>(() => {
    if (this.configRes.error()) return false;
    return this.configRes.value()?.registrationEnabled ?? null;
  });

  // Reading value() on an errored resource throws, so the error has to be checked
  // first here as it is above — otherwise a failed config read takes the whole screen
  // down instead of falling back to the local form.
  protected readonly providerAvailable = computed<boolean>(() => {
    if (this.configRes.error()) return false;
    return this.configRes.value()?.oidcAvailable ?? false;
  });

  /**
   * What to call the provider on the button. The operator names it; unset, the label
   * stays generic — the app never hard-codes which product it is talking to.
   */
  protected readonly providerName = computed<string>(() => {
    if (this.configRes.error()) return '';
    return this.configRes.value()?.oidc?.displayName?.trim() || 'your identity provider';
  });

  /**
   * False until /api/config has been answered (or has failed). The screen holds its
   * shape behind a placeholder rather than flashing a method the instance may not
   * offer, then swapping it out from under the visitor.
   */
  protected readonly configResolved = computed(() => this.localLoginEnabled() !== null);

  /** Neither door is open — the screen explains that instead of showing an empty card. */
  protected readonly noMethod = computed(
    () => this.localLoginEnabled() === false && !this.providerAvailable(),
  );

  /** Set when the API refused a provider sign-in we came back from. */
  protected readonly providerError = this.provider.error.asReadonly();

  protected readonly model = signal({ email: '', password: '' });
  protected readonly f = form(this.model, (p) => {
    required(p.email);
    email(p.email);
    required(p.password);
  });
  /** In-flight state belongs to the command, which the store owns. */
  protected readonly submitting = this.auth.pending;

  constructor() {
    // Root-provided store shared with the sibling screen: clear anything it left behind.
    this.auth.resetRequestStatus();

    effect(() => {
      if (this.auth.fulfilled()) void this.router.navigateByUrl('/');
    });

    // The store owns the message now — it is the only side that still sees the error.
    effect(() => {
      const message = this.auth.requestError();
      if (message) {
        this.toast.show(message, 'error');
        this.auth.resetRequestStatus();
      }
    });
  }

  /** Set on click and never cleared: the page is on its way out to the provider. */
  protected readonly redirecting = signal(false);

  protected signInWithProvider(): void {
    this.redirecting.set(true);
    this.provider.start();
  }

  protected onSubmit(): void {
    if (this.submitting()) return;
    if (this.f().invalid()) {
      // Surface why nothing happened: reveal every field's validation message.
      this.f().markAsTouched();
      return;
    }
    this.auth.login(this.model());
  }
}

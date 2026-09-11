import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { FormField, FormRoot, email, form, required } from '@angular/forms/signals';
import { Button, ToastService } from '@coffee-tracker/ui';
import { ConfigApi } from '@coffee-tracker/data';
import { AuthStore } from '../auth.store';
import { ProviderSignIn } from '../provider-sign-in';

@Component({
  selector: 'ct-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, FormRoot, RouterLink, Button],
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

  protected readonly providerAvailable = computed<boolean>(
    () => this.configRes.value()?.oidcAvailable ?? false,
  );

  /** Set when the API refused a provider sign-in we came back from. */
  protected readonly providerError = this.provider.error.asReadonly();

  protected readonly model = signal({ email: '', password: '' });
  protected readonly f = form(this.model, (p) => {
    required(p.email);
    email(p.email);
    required(p.password);
  });
  protected readonly submitting = signal(false);

  protected signInWithProvider(): void {
    this.provider.start();
  }

  protected async onSubmit(): Promise<void> {
    if (this.submitting()) return;
    if (this.f().invalid()) {
      // Surface why nothing happened: reveal every field's validation message.
      this.f().markAsTouched();
      return;
    }
    this.submitting.set(true);
    try {
      await this.auth.login(this.model());
      await this.router.navigateByUrl('/');
    } catch (err: unknown) {
      // 403 means the instance no longer accepts app accounts at all — telling the user
      // their password is wrong would send them round in circles.
      this.toast.show(
        (err as { status?: number })?.status === 403
          ? 'This instance no longer accepts sign-in with an app account. Use the identity provider.'
          : 'Invalid email or password.',
        'error',
      );
    } finally {
      this.submitting.set(false);
    }
  }
}

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
import { FormField, FormRoot, email, form, minLength, required } from '@angular/forms/signals';
import { Button, ToastService } from '@coffee-tracker/ui';
import { ConfigApi } from '@coffee-tracker/data';
import { AuthStore } from '../auth.store';

@Component({
  selector: 'ct-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, FormRoot, RouterLink, Button],
  templateUrl: './register.html',
})
export class Register {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly config = inject(ConfigApi);

  /** Reactive config read (same rxResource pattern as the data screens). */
  private readonly configRes = rxResource({ stream: () => this.config.get() });
  /** null = still loading the config flag; a failed load closes registration. */
  protected readonly registrationEnabled = computed<boolean | null>(() => {
    if (this.configRes.error()) return false;
    return this.configRes.value()?.registrationEnabled ?? null;
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
  protected readonly model = signal({ email: '', password: '', displayName: '' });
  protected readonly f = form(this.model, (p) => {
    required(p.email);
    email(p.email);
    required(p.displayName);
    required(p.password);
    minLength(p.password, 8);
  });

  protected onSubmit(): void {
    if (this.submitting()) return;
    if (this.f().invalid()) {
      // Surface why nothing happened: reveal every field's validation message.
      this.f().markAsTouched();
      return;
    }
    this.auth.register(this.model());
  }
}

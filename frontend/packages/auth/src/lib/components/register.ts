import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { FormField, FormRoot, email, form, minLength, required } from '@angular/forms/signals';
import { Button } from '@coffee-tracker/ui';
import { ConfigApi } from '@coffee-tracker/data';
import { AuthStore } from '../auth.store';

@Component({
  selector: 'ct-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormField, FormRoot, RouterLink, Button],
  templateUrl: './register.html',
})
export class Register {
  protected readonly auth = inject(AuthStore);
  private readonly config = inject(ConfigApi);

  /** Reactive config read (same rxResource pattern as the data screens). */
  private readonly configRes = rxResource({ stream: () => this.config.get() });
  /** null = still loading the config flag; a failed load closes registration. */
  protected readonly registrationEnabled = computed<boolean | null>(() => {
    if (this.configRes.error()) return false;
    return this.configRes.value()?.registrationEnabled ?? null;
  });
  protected readonly model = signal({ email: '', password: '', displayName: '' });
  protected readonly f = form(this.model, (p) => {
    required(p.email);
    email(p.email);
    required(p.displayName);
    required(p.password);
    minLength(p.password, 8);
  });

  constructor() {
    // AuthStore is root-provided, so Login and Register share one requestStatus. Without
    // this, a sign-in still in flight renders the sibling screen's button disabled, and a
    // failed one leaves its error sitting there.
    this.auth.resetRequestStatus();
  }

  protected onSubmit(): void {
    if (this.auth.pending()) return;
    if (this.f().invalid()) {
      // Surface why nothing happened: reveal every field's validation message.
      this.f().markAsTouched();
      return;
    }
    this.auth.register(this.model());
  }
}

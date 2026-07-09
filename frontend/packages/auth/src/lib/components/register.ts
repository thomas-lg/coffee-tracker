import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
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
  protected readonly submitting = signal(false);
  protected readonly model = signal({ email: '', password: '', displayName: '' });
  protected readonly f = form(this.model, (p) => {
    required(p.email);
    email(p.email);
    required(p.displayName);
    required(p.password);
    minLength(p.password, 8);
  });

  protected async onSubmit(): Promise<void> {
    if (this.submitting()) return;
    if (this.f().invalid()) {
      // Surface why nothing happened: reveal every field's validation message.
      this.f().markAsTouched();
      return;
    }
    this.submitting.set(true);
    try {
      await this.auth.register(this.model());
      await this.router.navigateByUrl('/');
    } catch {
      this.toast.show('Could not create the account — the email may already be in use.', 'error');
    } finally {
      this.submitting.set(false);
    }
  }
}

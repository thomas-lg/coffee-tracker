import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormField, FormRoot, email, form, minLength, required } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { Button, ToastService } from '@coffee-tracker/ui';
import { ConfigApi } from '@coffee-tracker/data';
import { AuthStore } from './auth.store';

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

  /** null = still loading the config flag. */
  protected readonly registrationEnabled = signal<boolean | null>(null);
  protected readonly submitting = signal(false);
  protected readonly model = signal({ email: '', password: '', displayName: '' });
  protected readonly f = form(this.model, (p) => {
    required(p.email);
    email(p.email);
    required(p.displayName);
    required(p.password);
    minLength(p.password, 8);
  });

  constructor() {
    firstValueFrom(this.config.get())
      .then((c) => this.registrationEnabled.set(c.registrationEnabled))
      .catch(() => this.registrationEnabled.set(false));
  }

  protected async onSubmit(): Promise<void> {
    if (this.submitting() || this.f().invalid()) return;
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

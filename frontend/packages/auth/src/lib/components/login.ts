import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormField, FormRoot, email, form, required } from '@angular/forms/signals';
import { Button, ToastService } from '@coffee-tracker/ui';
import { AuthStore } from '../auth.store';

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

  protected readonly model = signal({ email: '', password: '' });
  protected readonly f = form(this.model, (p) => {
    required(p.email);
    email(p.email);
    required(p.password);
  });
  protected readonly submitting = signal(false);

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
    } catch {
      this.toast.show('Invalid email or password.', 'error');
    } finally {
      this.submitting.set(false);
    }
  }
}

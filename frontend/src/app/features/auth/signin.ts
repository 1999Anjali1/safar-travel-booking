import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstError } from '../../core/api';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-signin',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="container auth">
      <form class="auth__card" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <h1>Sign in</h1>
        <p class="muted">Use your Safar account to book and manage trips.</p>

        @if (error()) {
          <div class="notice notice--error" role="alert">{{ error() }}</div>
        }

        <label class="field">
          <span>Email</span>
          <input type="email" formControlName="email" autocomplete="username" />
        </label>
        <label class="field">
          <span>Password</span>
          <input type="password" formControlName="password" autocomplete="current-password" />
        </label>
        <button type="submit" class="btn btn--primary btn--block" [disabled]="busy()">{{ busy() ? 'Signing in…' : 'Sign in' }}</button>

        <p class="auth__switch">New to Safar? <a routerLink="/register" [queryParams]="{ returnUrl: returnUrl() }">Create an account</a></p>

        <div class="demo">
          <p>Demo accounts, password <code>Password&#64;123</code></p>
          <div class="demo__buttons">
            <button type="button" class="btn btn--ghost btn--small" (click)="demo('customer@safar.qa')">Customer</button>
            <button type="button" class="btn btn--ghost btn--small" (click)="demo('agent@safar.qa')">Travel agent</button>
            <button type="button" class="btn btn--ghost btn--small" (click)="demo('admin@safar.qa')">Admin</button>
          </div>
        </div>
      </form>
    </div>
  `,
})
export class SignIn {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly returnUrl = input('', { transform: (v: string | undefined) => v ?? '' });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  demo(email: string): void {
    this.form.setValue({ email, password: 'Password@123' });
    this.submit();
  }

  submit(): void {
    if (this.form.invalid) {
      this.error.set('Enter your email and password.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: (user) => {
        const fallback = user.role === 'Customer' ? '/' : '/admin';
        this.router.navigateByUrl(this.returnUrl() || fallback);
      },
      error: (e) => {
        this.error.set(firstError(e));
        this.busy.set(false);
      },
    });
  }
}

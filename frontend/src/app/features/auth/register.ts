import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { errorMessages } from '../../core/api';
import { AuthService } from '../../core/auth.service';

@Component({
  selector: 'app-register',
  imports: [ReactiveFormsModule, RouterLink],
  template: `
    <div class="container auth">
      <form class="auth__card" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <h1>Create your account</h1>
        <p class="muted">Book trips, keep your booking references in one place and cancel online.</p>

        @if (errors().length) {
          <div class="notice notice--error" role="alert">
            @for (e of errors(); track $index) { <p>{{ e }}</p> }
          </div>
        }

        <label class="field">
          <span>Full name</span>
          <input formControlName="fullName" autocomplete="name" />
        </label>
        <label class="field">
          <span>Email</span>
          <input type="email" formControlName="email" autocomplete="email" />
          @if (form.controls.email.touched && form.controls.email.invalid) {
            <small class="field__error">Enter a valid email address.</small>
          }
        </label>
        <label class="field">
          <span>Mobile number <em>(optional)</em></span>
          <input type="tel" formControlName="phone" autocomplete="tel" placeholder="+974 5555 5555" />
        </label>
        <label class="field">
          <span>Password</span>
          <input type="password" formControlName="password" autocomplete="new-password" />
          <small class="field__hint">At least 8 characters with an uppercase letter and a number.</small>
        </label>
        <button type="submit" class="btn btn--primary btn--block" [disabled]="busy()">{{ busy() ? 'Creating account…' : 'Create account' }}</button>
        <p class="auth__switch">Already have an account? <a routerLink="/signin" [queryParams]="{ returnUrl: returnUrl() }">Sign in</a></p>
      </form>
    </div>
  `,
})
export class Register {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly returnUrl = input('', { transform: (v: string | undefined) => v ?? '' });

  protected readonly busy = signal(false);
  protected readonly errors = signal<string[]>([]);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    fullName: ['', [Validators.required, Validators.minLength(3)]],
    email: ['', [Validators.required, Validators.email]],
    phone: [''],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.errors.set(['Fill in your name, email and a password of at least 8 characters.']);
      return;
    }
    this.busy.set(true);
    this.errors.set([]);
    const v = this.form.getRawValue();
    this.auth.register(v.fullName.trim(), v.email.trim(), v.password, v.phone.trim() || null).subscribe({
      next: () => this.router.navigateByUrl(this.returnUrl() || '/'),
      error: (e) => {
        this.errors.set(errorMessages(e));
        this.busy.set(false);
      },
    });
  }
}

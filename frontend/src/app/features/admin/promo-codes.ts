import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminApi, errorMessages } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { PromoCode } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { QarPipe } from '../../shared/ui';

@Component({
  selector: 'app-promo-codes',
  imports: [ReactiveFormsModule, DatePipe, QarPipe],
  template: `
    <header class="admin-head"><h1>Promo codes</h1></header>

    @if (errors().length) {
      <div class="notice notice--error">@for (e of errors(); track $index) { <p>{{ e }}</p> }</div>
    }

    <div class="panel panel--flush">
      <table class="table">
        <thead><tr><th>Code</th><th>Description</th><th class="num">Discount</th><th class="num">Used</th><th>Valid until</th><th>Status</th><th></th></tr></thead>
        <tbody>
          @for (p of codes(); track p.id) {
            <tr [class.is-muted]="!p.isActive">
              <td class="ref">{{ p.code }}</td>
              <td>{{ p.description }}</td>
              <td class="num">{{ p.discountPercent }}%@if (p.maxDiscount) { <span class="muted"> up to {{ p.maxDiscount | qar }}</span> }</td>
              <td class="num">{{ p.usedCount }}@if (p.maxUses) { / {{ p.maxUses }} }</td>
              <td>{{ p.validUntil | date: 'd MMM y' }}</td>
              <td><span [class]="p.isActive ? 'pill pill--on' : 'pill'">{{ p.isActive ? 'Active' : 'Off' }}</span></td>
              <td class="row-actions">
                @if (isAdmin) {
                  <button type="button" class="link-button" (click)="toggle(p)">{{ p.isActive ? 'Turn off' : 'Turn on' }}</button>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>

    @if (isAdmin) {
      <form class="panel" [formGroup]="form" (ngSubmit)="create()">
        <h2>Create a promo code</h2>
        <div class="fields">
          <label class="field"><span>Code</span><input formControlName="code" placeholder="EIDTRAVEL" /></label>
          <label class="field field--wide"><span>Description</span><input formControlName="description" placeholder="Eid holiday offer" /></label>
          <label class="field"><span>Discount %</span><input type="number" formControlName="discountPercent" min="1" max="50" /></label>
          <label class="field"><span>Maximum discount, QAR <em>(optional)</em></span><input type="number" formControlName="maxDiscount" /></label>
          <label class="field"><span>Valid until</span><input type="date" formControlName="validUntil" /></label>
          <label class="field"><span>Usage limit <em>(optional)</em></span><input type="number" formControlName="maxUses" /></label>
        </div>
        <div class="actions"><button type="submit" class="btn btn--primary">Create code</button></div>
      </form>
    } @else {
      <p class="muted">Only admins can create or change promo codes.</p>
    }
  `,
})
export class PromoCodes {
  private readonly api = inject(AdminApi);
  private readonly toasts = inject(ToastService);
  protected readonly isAdmin = inject(AuthService).user()?.role === 'Admin';

  protected readonly codes = signal<PromoCode[]>([]);
  protected readonly errors = signal<string[]>([]);
  protected readonly form = inject(FormBuilder).nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^[A-Za-z0-9]{3,30}$/)]],
    description: ['', Validators.required],
    discountPercent: [10, [Validators.required, Validators.min(1), Validators.max(50)]],
    maxDiscount: [null as number | null],
    validUntil: ['', Validators.required],
    maxUses: [null as number | null],
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.api.promoCodes().subscribe((c) => this.codes.set(c));
  }

  toggle(p: PromoCode): void {
    this.api.setPromoActive(p.id, !p.isActive).subscribe(() => this.load());
  }

  create(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.errors.set(['Code (letters and numbers), description, discount and expiry date are required.']);
      return;
    }
    const v = this.form.getRawValue();
    this.api
      .createPromoCode({ ...v, code: v.code.toUpperCase(), validUntil: new Date(v.validUntil + 'T23:59:59Z').toISOString() })
      .subscribe({
        next: (p) => {
          this.toasts.show(`${p.code} created.`, 'success');
          this.form.reset({ discountPercent: 10 });
          this.errors.set([]);
          this.load();
        },
        error: (e) => this.errors.set(errorMessages(e)),
      });
  }
}

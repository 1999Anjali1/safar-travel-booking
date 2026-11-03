import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminApi, errorMessages, firstError } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { AdminDeparture, AdminPackageDetail, CATEGORIES, PackageCategory, UpsertPackageRequest } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { CATEGORY_LABELS, QarPipe } from '../../shared/ui';

@Component({
  selector: 'app-package-editor',
  imports: [ReactiveFormsModule, FormsModule, RouterLink, DatePipe, QarPipe],
  templateUrl: './package-editor.html',
})
export class PackageEditor implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);
  private readonly toasts = inject(ToastService);
  private readonly fb = inject(FormBuilder).nonNullable;
  protected readonly auth = inject(AuthService);

  /** Route param: a number, or "new". */
  readonly id = input.required<string>();
  protected readonly isNew = computed(() => this.id() === 'new');

  protected readonly pkg = signal<AdminPackageDetail | null>(null);
  protected readonly errors = signal<string[]>([]);
  protected readonly saving = signal(false);
  protected readonly categories = CATEGORIES;
  protected readonly labels = CATEGORY_LABELS;

  protected readonly form = this.fb.group({
    title: ['', [Validators.required, Validators.minLength(5)]],
    summary: ['', Validators.required],
    description: ['', Validators.required],
    destination: ['', Validators.required],
    country: ['', Validators.required],
    category: ['City' as PackageCategory, Validators.required],
    durationDays: [1, [Validators.required, Validators.min(1), Validators.max(30)]],
    basePrice: [0, [Validators.required, Validators.min(1)]],
    imageUrl: [''],
    highlights: [''],
    inclusions: [''],
  });

  protected readonly departureForm = this.fb.group({
    startDate: ['', Validators.required],
    capacity: [20, [Validators.required, Validators.min(1), Validators.max(200)]],
    priceOverride: [null as number | null],
  });

  protected cancellingId: number | null = null;
  protected cancelReason = '';

  protected readonly minDate = new Date(Date.now() + 86400000).toISOString().slice(0, 10);

  ngOnInit(): void {
    if (!this.isNew()) {
      this.api.package(Number(this.id())).subscribe({
        next: (p) => this.setPackage(p),
        error: (e) => this.errors.set([firstError(e)]),
      });
    }
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.errors.set(['Fill in all required fields.']);
      return;
    }
    const v = this.form.getRawValue();
    const lines = (text: string) => text.split('\n').map((l) => l.trim()).filter(Boolean);
    const body: UpsertPackageRequest = {
      ...v,
      imageUrl: v.imageUrl.trim() || null,
      highlights: lines(v.highlights),
      inclusions: lines(v.inclusions),
    };

    this.saving.set(true);
    this.errors.set([]);
    const request = this.isNew() ? this.api.createPackage(body) : this.api.updatePackage(Number(this.id()), body);
    request.subscribe({
      next: (p) => {
        this.saving.set(false);
        this.toasts.show(this.isNew() ? 'Package created as a draft. Add departures, then publish it.' : 'Changes saved.', 'success');
        if (this.isNew()) this.router.navigate(['/admin/packages', p.id]);
        else this.setPackage(p);
      },
      error: (e) => {
        this.errors.set(errorMessages(e));
        this.saving.set(false);
      },
    });
  }

  togglePublished(): void {
    const p = this.pkg();
    if (!p) return;
    this.api.setPublished(p.id, !p.isPublished).subscribe({
      next: (updated) => {
        this.pkg.set(updated);
        this.toasts.show(updated.isPublished ? 'Published. Customers can now book this trip.' : 'Unpublished. Hidden from customers.', 'info');
      },
      error: (e) => this.errors.set([firstError(e)]),
    });
  }

  addDeparture(): void {
    const p = this.pkg();
    if (!p || this.departureForm.invalid) {
      this.departureForm.markAllAsTouched();
      return;
    }
    const v = this.departureForm.getRawValue();
    this.api.addDeparture(p.id, v.startDate, v.capacity, v.priceOverride || null).subscribe({
      next: (updated) => {
        this.pkg.set(updated);
        this.departureForm.patchValue({ startDate: '', priceOverride: null });
        this.toasts.show('Departure added.', 'success');
      },
      error: (e) => this.errors.set(errorMessages(e)),
    });
  }

  confirmCancelDeparture(d: AdminDeparture): void {
    if (!this.cancelReason.trim()) return;
    this.api.cancelDeparture(d.id, this.cancelReason.trim()).subscribe({
      next: (updated) => {
        this.pkg.set(updated);
        this.toasts.show(`Departure cancelled. ${d.confirmedBookings + d.pendingBookings} booking(s) cancelled and fully refunded.`, 'info');
        this.cancellingId = null;
        this.cancelReason = '';
      },
      error: (e) => this.errors.set([firstError(e)]),
    });
  }

  isPast(d: AdminDeparture): boolean {
    return d.startDate < new Date().toISOString().slice(0, 10);
  }

  private setPackage(p: AdminPackageDetail): void {
    this.pkg.set(p);
    this.form.patchValue({
      ...p,
      imageUrl: p.imageUrl ?? '',
      highlights: p.highlights.join('\n'),
      inclusions: p.inclusions.join('\n'),
    });
  }
}

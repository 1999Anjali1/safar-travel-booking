import { DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { FormArray, FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { BookingApi, CatalogApi, errorMessages, firstError } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { Departure, PackageDetail, Quote } from '../../core/models';
import { Cover, QarPipe } from '../../shared/ui';

@Component({
  selector: 'app-checkout',
  imports: [ReactiveFormsModule, RouterLink, DatePipe, QarPipe, Cover],
  templateUrl: './checkout.html',
})
export class Checkout implements OnInit {
  private readonly catalog = inject(CatalogApi);
  private readonly bookings = inject(BookingApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder).nonNullable;

  readonly departureId = input.required<string>();
  readonly seats = input('1', { transform: (v: string | undefined) => v ?? '1' });
  readonly trip = input('', { transform: (v: string | undefined) => v ?? '' });

  protected readonly pkg = signal<PackageDetail | null>(null);
  protected readonly departure = signal<Departure | null>(null);
  protected readonly quote = signal<Quote | null>(null);
  protected readonly appliedPromo = signal<string | null>(null);
  protected readonly promoMessage = signal<{ text: string; ok: boolean } | null>(null);
  protected readonly errors = signal<string[]>([]);
  protected readonly submitting = signal(false);

  protected readonly form = this.fb.group({
    travellers: this.fb.array([this.travellerGroup()]),
    contactEmail: ['', [Validators.required, Validators.email]],
    contactPhone: ['', [Validators.required, Validators.pattern(/^\+?[0-9 ]{7,20}$/)]],
    specialRequests: ['', Validators.maxLength(500)],
    promoCode: [''],
  });

  protected get travellers(): FormArray {
    return this.form.controls.travellers;
  }

  protected readonly maxTravellers = computed(() => Math.min(10, this.departure()?.seatsAvailable ?? 10));

  ngOnInit(): void {
    const user = this.auth.user();
    this.form.patchValue({ contactEmail: user?.email ?? '', contactPhone: user?.phone ?? '' });

    const wanted = Math.max(1, Math.min(10, Number(this.seats()) || 1));
    for (let i = 1; i < wanted; i++) this.travellers.push(this.travellerGroup());
    if (user) this.travellers.at(0).patchValue({ fullName: user.fullName });

    this.catalog.get(this.trip()).subscribe({
      next: (p) => {
        this.pkg.set(p);
        const dep = p.departures.find((d) => d.id === Number(this.departureId())) ?? null;
        this.departure.set(dep);
        if (!dep) this.errors.set(['This departure is no longer available. Please choose another date.']);
        else this.refreshQuote();
      },
      error: (e) => this.errors.set([firstError(e)]),
    });
  }

  addTraveller(): void {
    if (this.travellers.length >= this.maxTravellers()) return;
    this.travellers.push(this.travellerGroup());
    this.refreshQuote();
  }

  removeTraveller(index: number): void {
    if (this.travellers.length === 1) return;
    this.travellers.removeAt(index);
    this.refreshQuote();
  }

  applyPromo(): void {
    const code = this.form.controls.promoCode.value.trim();
    this.appliedPromo.set(code || null);
    this.refreshQuote(true);
  }

  removePromo(): void {
    this.form.controls.promoCode.setValue('');
    this.appliedPromo.set(null);
    this.promoMessage.set(null);
    this.refreshQuote();
  }

  private refreshQuote(showPromoResult = false): void {
    const dep = this.departure();
    if (!dep) return;
    this.bookings.quote(dep.id, this.travellers.length, this.appliedPromo()).subscribe({
      next: (q) => {
        this.quote.set(q);
        if (showPromoResult && this.appliedPromo()) {
          const ok = !!q.promoCode;
          this.promoMessage.set({ text: q.promoMessage ?? '', ok });
          if (!ok) this.appliedPromo.set(null);
        }
      },
      error: (e) => this.errors.set(errorMessages(e)),
    });
  }

  submit(): void {
    const dep = this.departure();
    if (!dep) return;
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.errors.set(['Check the highlighted fields.']);
      return;
    }

    const v = this.form.getRawValue();
    this.submitting.set(true);
    this.errors.set([]);

    this.bookings
      .create({
        departureId: dep.id,
        travellers: v.travellers.map((t: { fullName: string; nationality: string; passportNumber: string; dateOfBirth: string }) => ({
          fullName: t.fullName.trim(),
          nationality: t.nationality.trim(),
          passportNumber: t.passportNumber.trim() || null,
          dateOfBirth: t.dateOfBirth || null,
        })),
        contactEmail: v.contactEmail.trim(),
        contactPhone: v.contactPhone.trim(),
        promoCode: this.appliedPromo(),
        specialRequests: v.specialRequests.trim() || null,
      })
      .subscribe({
        next: (booking) => this.router.navigate(['/bookings', booking.reference]),
        error: (e) => {
          this.errors.set(errorMessages(e));
          this.submitting.set(false);
          this.refreshQuote(); // seat count may have changed
        },
      });
  }

  invalid(path: string, index?: number): boolean {
    const control = index === undefined ? this.form.get(path) : this.travellers.at(index).get(path);
    return !!control && control.invalid && control.touched;
  }

  private travellerGroup() {
    return this.fb.group({
      fullName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(100)]],
      nationality: ['', [Validators.required, Validators.maxLength(60)]],
      passportNumber: ['', [Validators.maxLength(20), Validators.pattern(/^[A-Za-z0-9]*$/)]],
      dateOfBirth: [''],
    });
  }
}

import { DatePipe } from '@angular/common';
import { Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { BookingApi, firstError } from '../../core/api';
import { AuthService } from '../../core/auth.service';
import { Booking, CancellationQuote, PaymentMethod } from '../../core/models';
import { ToastService } from '../../core/toast.service';
import { QarPipe, StatusChip } from '../../shared/ui';

@Component({
  selector: 'app-booking-page',
  imports: [RouterLink, DatePipe, FormsModule, QarPipe, StatusChip],
  templateUrl: './booking-page.html',
})
export class BookingPage {
  private readonly api = inject(BookingApi);
  private readonly toasts = inject(ToastService);
  protected readonly auth = inject(AuthService);
  protected readonly router = inject(Router);

  readonly reference = input.required<string>();

  protected readonly booking = signal<Booking | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);
  protected readonly now = signal(Date.now());

  protected method: PaymentMethod = 'Card';
  protected readonly methods: { value: PaymentMethod; label: string }[] = [
    { value: 'Card', label: 'Debit or credit card' },
    { value: 'ApplePay', label: 'Apple Pay' },
    { value: 'GooglePay', label: 'Google Pay' },
  ];

  protected readonly cancelQuote = signal<CancellationQuote | null>(null);
  protected cancelReason = '';

  /** Seconds left on the 15-minute seat hold. */
  protected readonly secondsLeft = computed(() => {
    const b = this.booking();
    if (!b || b.status !== 'PendingPayment') return 0;
    return Math.max(0, Math.floor((new Date(b.expiresAt).getTime() - this.now()) / 1000));
  });
  protected readonly countdown = computed(() => {
    const s = this.secondsLeft();
    return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, '0')}`;
  });
  protected readonly isAdminView = computed(() => this.router.url.startsWith('/admin'));

  constructor() {
    const timer = setInterval(() => this.now.set(Date.now()), 1000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));

    effect(() => this.load(this.reference()));
  }

  load(reference: string): void {
    this.api.get(reference).subscribe({
      next: (b) => this.booking.set(b),
      error: (e) => this.error.set(firstError(e)),
    });
  }

  pay(): void {
    const b = this.booking();
    if (!b) return;
    this.busy.set(true);
    this.error.set(null);
    this.api.pay(b.reference, this.method).subscribe({
      next: (paid) => {
        this.booking.set(paid);
        this.busy.set(false);
        this.toasts.show(`Payment received. ${paid.reference} is confirmed.`, 'success');
      },
      error: (e) => {
        this.error.set(firstError(e));
        this.busy.set(false);
        this.load(b.reference);
      },
    });
  }

  startCancel(): void {
    const b = this.booking();
    if (!b) return;
    this.api.cancellationQuote(b.reference).subscribe({
      next: (q) => this.cancelQuote.set(q),
      error: (e) => this.error.set(firstError(e)),
    });
  }

  confirmCancel(): void {
    const b = this.booking();
    if (!b) return;
    this.busy.set(true);
    this.api.cancel(b.reference, this.cancelReason.trim() || null).subscribe({
      next: (cancelled) => {
        this.booking.set(cancelled);
        this.cancelQuote.set(null);
        this.busy.set(false);
        this.toasts.show(
          cancelled.refundAmount > 0
            ? `Booking cancelled. QAR ${cancelled.refundAmount} will be refunded to the original payment method.`
            : 'Booking cancelled.',
          'info',
        );
      },
      error: (e) => {
        this.error.set(firstError(e));
        this.busy.set(false);
      },
    });
  }
}

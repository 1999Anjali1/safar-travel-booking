import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AdminApi, firstError } from '../../core/api';
import { BookingListItem, BookingStatus, PagedResult } from '../../core/models';
import { QarPipe, STATUS_LABELS, StatusChip } from '../../shared/ui';

@Component({
  selector: 'app-admin-bookings',
  imports: [FormsModule, RouterLink, DatePipe, QarPipe, StatusChip],
  template: `
    <header class="admin-head">
      <h1>Bookings</h1>
      <p class="muted">{{ result()?.totalCount ?? 0 }} bookings</p>
    </header>

    <form class="toolbar" (ngSubmit)="go({ search: searchText.trim() || null })">
      <input name="search" [(ngModel)]="searchText" placeholder="Reference, customer, email or trip" class="toolbar__search" />
      <select name="status" [ngModel]="status()" (ngModelChange)="go({ status: $event || null })">
        <option value="">All statuses</option>
        @for (s of statuses; track s) {
          <option [value]="s">{{ labels[s] }}</option>
        }
      </select>
      <button type="submit" class="btn btn--ghost">Search</button>
    </form>

    @if (error()) {
      <div class="notice notice--error">{{ error() }}</div>
    }

    <div class="panel panel--flush">
      <table class="table">
        <thead>
          <tr><th>Reference</th><th>Customer</th><th>Trip</th><th>Departs</th><th class="num">Seats</th><th class="num">Total</th><th>Status</th><th>Booked</th></tr>
        </thead>
        <tbody>
          @for (b of result()?.items ?? []; track b.id) {
            <tr>
              <td><a [routerLink]="['/admin/bookings', b.reference]" class="ref">{{ b.reference }}</a></td>
              <td>{{ b.customerName }}</td>
              <td>{{ b.packageTitle }}</td>
              <td>{{ b.departureDate | date: 'd MMM y' }}</td>
              <td class="num">{{ b.seats }}</td>
              <td class="num">{{ b.total | qar }}</td>
              <td><app-status [status]="b.status" /></td>
              <td class="muted">{{ b.createdAt | date: 'd MMM, HH:mm' }}</td>
            </tr>
          } @empty {
            <tr><td colspan="8" class="table__empty">No bookings match.</td></tr>
          }
        </tbody>
      </table>
      @if (totalPages() > 1) {
        <nav class="pager pager--inline">
          <button type="button" class="btn btn--ghost btn--small" [disabled]="pageNumber() <= 1" (click)="go({ page: pageNumber() - 1 })">Previous</button>
          <span>Page {{ pageNumber() }} of {{ totalPages() }}</span>
          <button type="button" class="btn btn--ghost btn--small" [disabled]="pageNumber() >= totalPages()" (click)="go({ page: pageNumber() + 1 })">Next</button>
        </nav>
      }
    </div>
  `,
})
export class AdminBookings {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);

  readonly search = input('', { transform: (v: string | undefined) => v ?? '' });
  readonly status = input('', { transform: (v: BookingStatus | '' | undefined) => v ?? '' });
  readonly page = input('1', { transform: (v: string | undefined) => v ?? '1' });

  protected readonly statuses: BookingStatus[] = ['PendingPayment', 'Confirmed', 'Cancelled', 'Expired'];
  protected readonly labels = STATUS_LABELS;
  protected readonly result = signal<PagedResult<BookingListItem> | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly pageNumber = computed(() => Math.max(1, Number(this.page()) || 1));
  protected readonly totalPages = computed(() => this.result()?.totalPages ?? 1);
  protected searchText = '';

  constructor() {
    effect(() => {
      this.searchText = this.search();
      this.api.bookings({ search: this.search(), status: this.status(), page: this.pageNumber(), pageSize: 20 }).subscribe({
        next: (r) => this.result.set(r),
        error: (e) => this.error.set(firstError(e)),
      });
    });
  }

  go(changes: Record<string, string | number | null>): void {
    this.router.navigate([], { queryParams: { page: null, ...changes }, queryParamsHandling: 'merge' });
  }
}

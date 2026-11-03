import { DatePipe } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { catchError, of } from 'rxjs';
import { BookingApi } from '../../core/api';
import { BookingListItem } from '../../core/models';
import { QarPipe, StatusChip } from '../../shared/ui';

@Component({
  selector: 'app-my-trips',
  imports: [RouterLink, DatePipe, QarPipe, StatusChip],
  template: `
    <div class="container page">
      <h1>My trips</h1>

      @if (result() === undefined) {
        <p class="muted">Loading your bookings…</p>
      } @else if (upcoming().length === 0 && past().length === 0) {
        <div class="empty">
          <h2>No trips booked yet</h2>
          <p>When you book a trip it will appear here with your booking reference.</p>
          <a routerLink="/" class="btn btn--primary">Explore trips</a>
        </div>
      } @else {
        <h2 class="section-title">Coming up</h2>
        @if (upcoming().length === 0) {
          <p class="muted">Nothing booked for the coming weeks.</p>
        }
        <ul class="trip-list">
          @for (b of upcoming(); track b.id) {
            <li>
              <a [routerLink]="['/bookings', b.reference]" class="trip-row">
                <span class="trip-row__date">
                  <strong>{{ b.departureDate | date: 'd' }}</strong>
                  {{ b.departureDate | date: 'MMM y' }}
                </span>
                <span class="trip-row__main">
                  <strong>{{ b.packageTitle }}</strong>
                  <span class="muted">{{ b.reference }}, {{ b.seats }} {{ b.seats === 1 ? 'traveller' : 'travellers' }}</span>
                </span>
                <span class="trip-row__end">
                  <app-status [status]="b.status" />
                  <span>{{ b.total | qar }}</span>
                </span>
              </a>
            </li>
          }
        </ul>

        @if (past().length) {
          <h2 class="section-title">Past and cancelled</h2>
          <ul class="trip-list trip-list--muted">
            @for (b of past(); track b.id) {
              <li>
                <a [routerLink]="['/bookings', b.reference]" class="trip-row">
                  <span class="trip-row__date">
                    <strong>{{ b.departureDate | date: 'd' }}</strong>
                    {{ b.departureDate | date: 'MMM y' }}
                  </span>
                  <span class="trip-row__main">
                    <strong>{{ b.packageTitle }}</strong>
                    <span class="muted">{{ b.reference }}</span>
                  </span>
                  <span class="trip-row__end"><app-status [status]="b.status" /></span>
                </a>
              </li>
            }
          </ul>
        }
      }
    </div>
  `,
})
export class MyTrips {
  private readonly api = inject(BookingApi);

  protected readonly result = toSignal(this.api.mine(1, 50).pipe(catchError(() => of(null))));

  private readonly today = new Date().toISOString().slice(0, 10);

  private isUpcoming = (b: BookingListItem) =>
    b.departureDate >= this.today && (b.status === 'Confirmed' || b.status === 'PendingPayment');

  protected readonly upcoming = computed(() =>
    (this.result()?.items ?? []).filter(this.isUpcoming).sort((a, b) => a.departureDate.localeCompare(b.departureDate)),
  );
  protected readonly past = computed(() => (this.result()?.items ?? []).filter((b) => !this.isUpcoming(b)));
}

import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminApi, firstError } from '../../core/api';
import { Manifest } from '../../core/models';

/** Passenger list for the tour guide - printable. */
@Component({
  selector: 'app-manifest',
  imports: [RouterLink, DatePipe],
  template: `
    @if (manifest(); as m) {
      <a routerLink="/admin/packages" class="back no-print">All packages</a>
      <header class="admin-head">
        <div>
          <h1>Manifest: {{ m.packageTitle }}</h1>
          <p class="muted">Departs {{ m.startDate | date: 'EEEE d MMMM y' }}. {{ m.seatsBooked }} of {{ m.capacity }} seats sold.</p>
        </div>
        <button type="button" class="btn btn--ghost btn--small no-print" (click)="print()">Print</button>
      </header>
      <div class="panel panel--flush">
        <table class="table">
          <thead>
            <tr><th class="num">#</th><th>Traveller</th><th>Nationality</th><th>Passport</th><th>Date of birth</th><th>Booking</th><th>Phone</th><th>Requests</th></tr>
          </thead>
          <tbody>
            @for (t of m.travellers; track $index; let i = $index) {
              <tr>
                <td class="num">{{ i + 1 }}</td>
                <td>{{ t.fullName }}</td>
                <td>{{ t.nationality }}</td>
                <td>{{ t.passportNumber ?? 'Missing' }}</td>
                <td>{{ t.dateOfBirth ? (t.dateOfBirth | date: 'd MMM y') : '' }}</td>
                <td><a [routerLink]="['/admin/bookings', t.bookingReference]" class="ref">{{ t.bookingReference }}</a></td>
                <td>{{ t.contactPhone }}</td>
                <td class="muted">{{ t.specialRequests }}</td>
              </tr>
            } @empty {
              <tr><td colspan="8" class="table__empty">No confirmed travellers on this departure yet.</td></tr>
            }
          </tbody>
        </table>
      </div>
    } @else if (error()) {
      <div class="notice notice--error">{{ error() }}</div>
    }
  `,
})
export class ManifestPage {
  private readonly api = inject(AdminApi);
  readonly id = input.required<string>();
  protected readonly manifest = signal<Manifest | null>(null);
  protected readonly error = signal<string | null>(null);

  constructor() {
    effect(() =>
      this.api.manifest(Number(this.id())).subscribe({
        next: (m) => this.manifest.set(m),
        error: (e) => this.error.set(firstError(e)),
      }),
    );
  }

  print(): void {
    window.print();
  }
}

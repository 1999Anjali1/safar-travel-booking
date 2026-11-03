import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CatalogApi, firstError } from '../../core/api';
import { Departure, PackageDetail } from '../../core/models';
import { CATEGORY_LABELS, Cover, QarPipe } from '../../shared/ui';

@Component({
  selector: 'app-package-page',
  imports: [RouterLink, DatePipe, QarPipe, Cover],
  templateUrl: './package-page.html',
})
export class PackagePage {
  private readonly api = inject(CatalogApi);
  private readonly router = inject(Router);

  readonly slug = input.required<string>();

  protected readonly pkg = signal<PackageDetail | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly selectedId = signal<number | null>(null);
  protected readonly seats = signal(2);
  protected readonly categoryLabels = CATEGORY_LABELS;

  protected readonly selected = computed<Departure | null>(
    () => this.pkg()?.departures.find((d) => d.id === this.selectedId()) ?? null,
  );
  protected readonly maxSeats = computed(() => Math.min(10, this.selected()?.seatsAvailable ?? 10));
  protected readonly estimate = computed(() => (this.selected()?.pricePerPerson ?? 0) * this.seats());

  constructor() {
    effect(() => {
      const slug = this.slug();
      this.api.get(slug).subscribe({
        next: (p) => {
          this.pkg.set(p);
          // pre-select the first departure that can still be booked
          this.selectedId.set(p.departures.find((d) => d.isBookable)?.id ?? null);
        },
        error: (e) => this.error.set(firstError(e)),
      });
    });
  }

  select(d: Departure): void {
    if (!d.isBookable) return;
    this.selectedId.set(d.id);
    this.seats.update((s) => Math.min(s, Math.min(10, d.seatsAvailable)));
  }

  changeSeats(delta: number): void {
    this.seats.update((s) => Math.max(1, Math.min(this.maxSeats(), s + delta)));
  }

  seatsLabel(d: Departure): string {
    if (d.status === 'Cancelled') return 'Cancelled';
    if (d.seatsAvailable === 0) return 'Sold out';
    if (!d.isBookable) return 'Booking closed';
    return d.seatsAvailable <= 4 ? `${d.seatsAvailable} left` : `${d.seatsAvailable} seats`;
  }

  continue(): void {
    const d = this.selected();
    if (d) this.router.navigate(['/book', d.id], { queryParams: { seats: this.seats(), trip: this.slug() } });
  }
}

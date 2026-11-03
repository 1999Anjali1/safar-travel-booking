import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminApi, firstError } from '../../core/api';
import { Dashboard } from '../../core/models';
import { NotificationsService } from '../../core/notifications.service';
import { QarPipe, StatusChip } from '../../shared/ui';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

@Component({
  selector: 'app-admin-dashboard',
  imports: [RouterLink, DatePipe, DecimalPipe, QarPipe, StatusChip],
  templateUrl: './dashboard.html',
})
export class AdminDashboard {
  private readonly api = inject(AdminApi);
  protected readonly notifications = inject(NotificationsService);

  protected readonly data = signal<Dashboard | null>(null);
  protected readonly error = signal<string | null>(null);

  protected readonly monthChange = computed(() => {
    const d = this.data();
    if (!d || d.revenueLastMonth === 0) return null;
    return Math.round(((d.revenueThisMonth - d.revenueLastMonth) / d.revenueLastMonth) * 100);
  });

  /** Bar geometry for the revenue chart (SVG viewBox 600 x 220). */
  protected readonly bars = computed(() => {
    const months = this.data()?.revenueByMonth ?? [];
    const max = Math.max(1, ...months.map((m) => m.revenue));
    const chartHeight = 170;
    const slot = 600 / Math.max(1, months.length);
    return months.map((m, i) => {
      const h = Math.max(2, (m.revenue / max) * chartHeight);
      return {
        x: i * slot + slot * 0.2,
        width: slot * 0.6,
        y: 185 - h,
        height: h,
        label: MONTHS[m.month - 1],
        value: m.revenue,
        bookings: m.bookings,
        current: i === months.length - 1,
      };
    });
  });

  constructor() {
    this.load();
    // refresh the numbers whenever a live booking event arrives
    effect(() => {
      if (this.notifications.events().length) this.load();
    });
  }

  load(): void {
    this.api.dashboard().subscribe({
      next: (d) => this.data.set(d),
      error: (e) => this.error.set(firstError(e)),
    });
  }

  compact(value: number): string {
    return value >= 1_000_000 ? `${(value / 1_000_000).toFixed(1)}M` : value >= 1000 ? `${Math.round(value / 1000)}k` : `${value}`;
  }
}

import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { AdminApi } from '../../core/api';
import { CATEGORY_LABELS, QarPipe } from '../../shared/ui';

@Component({
  selector: 'app-admin-packages',
  imports: [RouterLink, QarPipe],
  template: `
    <header class="admin-head">
      <h1>Packages &amp; departures</h1>
      <a routerLink="/admin/packages/new" class="btn btn--primary btn--small">New package</a>
    </header>

    <div class="panel panel--flush">
      <table class="table">
        <thead>
          <tr><th>Package</th><th>Type</th><th class="num">Days</th><th class="num">Base price</th><th class="num">Upcoming dates</th><th class="num">Seats sold</th><th>Visibility</th></tr>
        </thead>
        <tbody>
          @for (p of packages() ?? []; track p.id) {
            <tr>
              <td>
                <a [routerLink]="['/admin/packages', p.id]"><strong>{{ p.title }}</strong></a>
                <div class="muted small">{{ p.destination }}, {{ p.country }}</div>
              </td>
              <td>{{ labels[p.category] }}</td>
              <td class="num">{{ p.durationDays }}</td>
              <td class="num">{{ p.basePrice | qar }}</td>
              <td class="num">{{ p.upcomingDepartures }}</td>
              <td class="num">{{ p.seatsSold }}</td>
              <td>
                <span [class]="p.isPublished ? 'pill pill--on' : 'pill'">{{ p.isPublished ? 'Published' : 'Draft' }}</span>
              </td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
})
export class AdminPackages {
  protected readonly packages = toSignal(inject(AdminApi).packages());
  protected readonly labels = CATEGORY_LABELS;
}

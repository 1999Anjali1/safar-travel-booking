import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { NotificationsService } from '../core/notifications.service';

@Component({
  selector: 'app-admin-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="admin">
      <aside class="admin__side">
        <a routerLink="/admin" class="wordmark wordmark--light">safar <small>staff</small></a>
        <nav class="admin__nav">
          <a routerLink="dashboard" routerLinkActive="is-active">Dashboard</a>
          <a routerLink="bookings" routerLinkActive="is-active">Bookings</a>
          <a routerLink="packages" routerLinkActive="is-active">Packages &amp; departures</a>
          <a routerLink="promo-codes" routerLinkActive="is-active">Promo codes</a>
        </nav>
        <div class="admin__foot">
          <span class="live" [class.live--on]="notifications.connected()">
            {{ notifications.connected() ? 'Live updates on' : 'Live updates off' }}
          </span>
          <div class="admin__user">
            <strong>{{ auth.user()?.fullName }}</strong>
            <span>{{ auth.user()?.role }}</span>
          </div>
          <a routerLink="/" class="admin__link">View customer site</a>
          <button type="button" class="admin__link" (click)="auth.logout()">Sign out</button>
        </div>
      </aside>
      <section class="admin__main">
        <router-outlet />
      </section>
    </div>
  `,
})
export class AdminShell implements OnInit, OnDestroy {
  protected readonly auth = inject(AuthService);
  protected readonly notifications = inject(NotificationsService);

  ngOnInit(): void {
    this.notifications.connect();
  }

  ngOnDestroy(): void {
    this.notifications.disconnect();
  }
}

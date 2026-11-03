import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
  selector: 'app-site-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="site-header">
      <div class="site-header__inner">
        <a routerLink="/" class="wordmark" aria-label="Safar home">safar</a>
        <nav class="site-nav">
          <a routerLink="/" routerLinkActive="is-active" [routerLinkActiveOptions]="{ exact: true }">Explore trips</a>
          @if (auth.isSignedIn()) {
            <a routerLink="/my-trips" routerLinkActive="is-active">My trips</a>
            @if (auth.isStaff()) {
              <a routerLink="/admin">Staff portal</a>
            }
            <span class="site-nav__user">{{ auth.user()?.fullName }}</span>
            <button type="button" class="link-button" (click)="auth.logout()">Sign out</button>
          } @else {
            <a routerLink="/signin" routerLinkActive="is-active">Sign in</a>
            <a routerLink="/register" class="btn btn--small btn--light">Create account</a>
          }
        </nav>
      </div>
    </header>

    <main>
      <router-outlet />
    </main>

    <footer class="site-footer">
      <div class="container site-footer__inner">
        <span class="wordmark wordmark--muted">safar</span>
        <p>Licensed tour operator, Doha. A portfolio project built with Angular and ASP.NET Core - payments are simulated.</p>
      </div>
    </footer>
  `,
})
export class SiteShell {
  protected readonly auth = inject(AuthService);
}

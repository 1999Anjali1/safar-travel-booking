import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, finalize, map, shareReplay, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuthResponse, User } from './models';

const STORAGE_KEY = 'safar.auth';

interface Session {
  accessToken: string;
  refreshToken: string;
  user: User;
}

/**
 * Holds the signed-in session in a signal.
 * Access tokens are short-lived (15 min); the interceptor calls `refresh()` when one expires.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly base = `${environment.apiUrl}/auth`;

  private readonly session = signal<Session | null>(this.restore());
  private refreshInFlight$: Observable<string> | null = null;

  readonly user = computed(() => this.session()?.user ?? null);
  readonly isSignedIn = computed(() => this.session() !== null);
  readonly isStaff = computed(() => ['Agent', 'Admin'].includes(this.user()?.role ?? ''));
  readonly accessToken = computed(() => this.session()?.accessToken ?? null);

  login(email: string, password: string): Observable<User> {
    return this.http.post<AuthResponse>(`${this.base}/login`, { email, password }).pipe(
      tap((res) => this.store(res)),
      map((res) => res.user),
    );
  }

  register(fullName: string, email: string, password: string, phone: string | null): Observable<User> {
    return this.http.post<AuthResponse>(`${this.base}/register`, { fullName, email, password, phone }).pipe(
      tap((res) => this.store(res)),
      map((res) => res.user),
    );
  }

  /** Exchanges the refresh token for a new pair. Concurrent callers share one request. */
  refresh(): Observable<string> {
    const refreshToken = this.session()?.refreshToken;
    if (!refreshToken) throw new Error('No refresh token');

    this.refreshInFlight$ ??= this.http.post<AuthResponse>(`${this.base}/refresh`, { refreshToken }).pipe(
      tap((res) => this.store(res)),
      map((res) => res.accessToken),
      finalize(() => (this.refreshInFlight$ = null)),
      shareReplay(1),
    );
    return this.refreshInFlight$;
  }

  logout(redirect = true): void {
    const refreshToken = this.session()?.refreshToken;
    if (refreshToken) this.http.post(`${this.base}/logout`, { refreshToken }).subscribe({ error: () => {} });
    this.clear();
    if (redirect) this.router.navigateByUrl('/');
  }

  clear(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
  }

  private store(res: AuthResponse): void {
    const session: Session = { accessToken: res.accessToken, refreshToken: res.refreshToken, user: res.user };
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    this.session.set(session);
  }

  private restore(): Session | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as Session) : null;
    } catch {
      return null;
    }
  }
}

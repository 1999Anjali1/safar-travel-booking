import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const withToken = (req: HttpRequest<unknown>, token: string | null) =>
  token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

/**
 * 1. Attaches the access token.
 * 2. On 401, silently refreshes the token once and retries the original request.
 * 3. If the refresh also fails, signs the user out and sends them to the login page.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const isAuthCall = req.url.includes('/auth/login') || req.url.includes('/auth/refresh') || req.url.includes('/auth/register');

  return next(withToken(req, auth.accessToken())).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401 || isAuthCall || !auth.isSignedIn()) return throwError(() => err);

      return auth.refresh().pipe(
        switchMap((token) => next(withToken(req, token))),
        catchError((refreshErr) => {
          auth.clear();
          router.navigate(['/signin'], { queryParams: { returnUrl: router.url } });
          return throwError(() => refreshErr);
        }),
      );
    }),
  );
};

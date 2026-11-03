import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Signed-in users only; others go to sign-in and come back afterwards. */
export const signedInGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return auth.isSignedIn() || inject(Router).createUrlTree(['/signin'], { queryParams: { returnUrl: state.url } });
};

/** Travel agents and admins only. */
export const staffGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isSignedIn()) return router.createUrlTree(['/signin'], { queryParams: { returnUrl: state.url } });
  return auth.isStaff() || router.createUrlTree(['/']);
};

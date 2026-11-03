import { Routes } from '@angular/router';
import { signedInGuard, staffGuard } from './core/guards';
import { AdminShell } from './layout/admin-shell';
import { SiteShell } from './layout/site-shell';

export const routes: Routes = [
  // ---------- customer site ----------
  {
    path: '',
    component: SiteShell,
    children: [
      {
        path: '',
        title: 'Safar - Tours from Doha',
        loadComponent: () => import('./features/catalog/explore').then((m) => m.Explore),
      },
      {
        path: 'trips/:slug',
        title: 'Safar - Trip details',
        loadComponent: () => import('./features/catalog/package-page').then((m) => m.PackagePage),
      },
      {
        path: 'book/:departureId',
        title: 'Safar - Traveller details',
        canActivate: [signedInGuard],
        loadComponent: () => import('./features/booking/checkout').then((m) => m.Checkout),
      },
      {
        path: 'bookings/:reference',
        title: 'Safar - Your booking',
        canActivate: [signedInGuard],
        loadComponent: () => import('./features/booking/booking-page').then((m) => m.BookingPage),
      },
      {
        path: 'my-trips',
        title: 'Safar - My trips',
        canActivate: [signedInGuard],
        loadComponent: () => import('./features/account/my-trips').then((m) => m.MyTrips),
      },
      {
        path: 'signin',
        title: 'Safar - Sign in',
        loadComponent: () => import('./features/auth/signin').then((m) => m.SignIn),
      },
      {
        path: 'register',
        title: 'Safar - Create account',
        loadComponent: () => import('./features/auth/register').then((m) => m.Register),
      },
    ],
  },

  // ---------- staff portal ----------
  {
    path: 'admin',
    component: AdminShell,
    canActivate: [staffGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Safar Admin - Dashboard',
        loadComponent: () => import('./features/admin/dashboard').then((m) => m.AdminDashboard),
      },
      {
        path: 'bookings',
        title: 'Safar Admin - Bookings',
        loadComponent: () => import('./features/admin/bookings').then((m) => m.AdminBookings),
      },
      {
        path: 'bookings/:reference',
        title: 'Safar Admin - Booking',
        loadComponent: () => import('./features/booking/booking-page').then((m) => m.BookingPage),
      },
      {
        path: 'packages',
        title: 'Safar Admin - Packages',
        loadComponent: () => import('./features/admin/packages').then((m) => m.AdminPackages),
      },
      {
        path: 'packages/:id',
        title: 'Safar Admin - Package',
        loadComponent: () => import('./features/admin/package-editor').then((m) => m.PackageEditor),
      },
      {
        path: 'departures/:id/manifest',
        title: 'Safar Admin - Manifest',
        loadComponent: () => import('./features/admin/manifest').then((m) => m.ManifestPage),
      },
      {
        path: 'promo-codes',
        title: 'Safar Admin - Promo codes',
        loadComponent: () => import('./features/admin/promo-codes').then((m) => m.PromoCodes),
      },
    ],
  },

  { path: '**', redirectTo: '' },
];

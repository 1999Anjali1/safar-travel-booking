import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../environments/environment';
import {
  AdminPackageDetail,
  AdminPackageListItem,
  Booking,
  BookingListItem,
  BookingStatus,
  CancellationQuote,
  CatalogFilters,
  CreateBookingRequest,
  Dashboard,
  Manifest,
  PackageCard,
  PackageDetail,
  PackageQuery,
  PagedResult,
  PaymentMethod,
  PromoCode,
  Quote,
  UpsertPackageRequest,
} from './models';

/** Builds HttpParams from an object, skipping empty values. */
function toParams(query: object): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== null && value !== undefined && value !== '') params = params.set(key, String(value));
  }
  return params;
}

@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/packages`;

  search(query: PackageQuery) {
    return this.http.get<PagedResult<PackageCard>>(this.base, { params: toParams(query) });
  }

  filters() {
    return this.http.get<CatalogFilters>(`${this.base}/filters`);
  }

  get(slug: string) {
    return this.http.get<PackageDetail>(`${this.base}/${slug}`);
  }
}

@Injectable({ providedIn: 'root' })
export class BookingApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/bookings`;

  quote(departureId: number, seats: number, promoCode?: string | null) {
    return this.http.post<Quote>(`${this.base}/quote`, { departureId, seats, promoCode: promoCode || null });
  }

  create(request: CreateBookingRequest) {
    return this.http.post<Booking>(this.base, request);
  }

  get(reference: string) {
    return this.http.get<Booking>(`${this.base}/${reference}`);
  }

  mine(page = 1, pageSize = 20) {
    return this.http.get<PagedResult<BookingListItem>>(`${this.base}/mine`, { params: toParams({ page, pageSize }) });
  }

  pay(reference: string, method: PaymentMethod) {
    return this.http.post<Booking>(`${this.base}/${reference}/pay`, { method });
  }

  cancellationQuote(reference: string) {
    return this.http.get<CancellationQuote>(`${this.base}/${reference}/cancellation-quote`);
  }

  cancel(reference: string, reason: string | null) {
    return this.http.post<Booking>(`${this.base}/${reference}/cancel`, { reason });
  }
}

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiUrl}/admin`;

  dashboard() {
    return this.http.get<Dashboard>(`${this.base}/dashboard`);
  }

  bookings(query: { status?: BookingStatus | ''; search?: string; page?: number; pageSize?: number }) {
    return this.http.get<PagedResult<BookingListItem>>(`${this.base}/bookings`, { params: toParams(query) });
  }

  packages() {
    return this.http.get<AdminPackageListItem[]>(`${this.base}/packages`);
  }

  package(id: number) {
    return this.http.get<AdminPackageDetail>(`${this.base}/packages/${id}`);
  }

  createPackage(body: UpsertPackageRequest) {
    return this.http.post<AdminPackageDetail>(`${this.base}/packages`, body);
  }

  updatePackage(id: number, body: UpsertPackageRequest) {
    return this.http.put<AdminPackageDetail>(`${this.base}/packages/${id}`, body);
  }

  setPublished(id: number, published: boolean) {
    return this.http.post<AdminPackageDetail>(`${this.base}/packages/${id}/${published ? 'publish' : 'unpublish'}`, {});
  }

  addDeparture(packageId: number, startDate: string, capacity: number, priceOverride: number | null) {
    return this.http.post<AdminPackageDetail>(`${this.base}/packages/${packageId}/departures`, {
      startDate,
      capacity,
      priceOverride,
    });
  }

  cancelDeparture(departureId: number, reason: string) {
    return this.http.post<AdminPackageDetail>(`${this.base}/departures/${departureId}/cancel`, { reason });
  }

  manifest(departureId: number) {
    return this.http.get<Manifest>(`${this.base}/departures/${departureId}/manifest`);
  }

  promoCodes() {
    return this.http.get<PromoCode[]>(`${this.base}/promo-codes`);
  }

  createPromoCode(body: Omit<PromoCode, 'id' | 'usedCount' | 'isActive'>) {
    return this.http.post<PromoCode>(`${this.base}/promo-codes`, body);
  }

  setPromoActive(id: number, active: boolean) {
    return this.http.post<PromoCode>(`${this.base}/promo-codes/${id}/${active ? 'activate' : 'deactivate'}`, {});
  }
}

/** Readable messages from an API error (ProblemDetails, validation dictionary or network failure). */
export function errorMessages(err: unknown): string[] {
  if (!(err instanceof HttpErrorResponse)) return ['Something went wrong.'];
  if (err.status === 0) return ['Cannot reach the Safar API. Check that the backend is running.'];
  if (err.status === 429) return ['Too many attempts. Wait a minute and try again.'];

  const body = err.error;
  if (body?.errors && typeof body.errors === 'object') {
    return Object.values(body.errors as Record<string, string[]>).flat();
  }
  return [body?.title ?? body?.detail ?? err.message];
}

export const firstError = (err: unknown) => errorMessages(err)[0];

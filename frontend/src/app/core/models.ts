// TypeScript mirrors of the API contracts. Enums arrive from .NET as strings.

export type UserRole = 'Customer' | 'Agent' | 'Admin';
export type PackageCategory = 'City' | 'Desert' | 'Beach' | 'Adventure' | 'Cultural' | 'Honeymoon';
export type DepartureStatus = 'Scheduled' | 'Cancelled';
export type BookingStatus = 'PendingPayment' | 'Confirmed' | 'Cancelled' | 'Expired';
export type PaymentStatus = 'Succeeded' | 'Failed' | 'Refunded';
export type PaymentMethod = 'Card' | 'ApplePay' | 'GooglePay';

export const CATEGORIES: PackageCategory[] = ['City', 'Desert', 'Beach', 'Adventure', 'Cultural', 'Honeymoon'];

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ---------- auth ----------

export interface User {
  id: string;
  fullName: string;
  email: string;
  phone: string | null;
  role: UserRole;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  user: User;
}

// ---------- catalogue ----------

export interface PackageCard {
  id: number;
  slug: string;
  title: string;
  summary: string;
  destination: string;
  country: string;
  category: PackageCategory;
  durationDays: number;
  fromPrice: number;
  imageUrl: string | null;
  nextDeparture: string | null;
  upcomingDepartures: number;
}

export interface Departure {
  id: number;
  startDate: string;
  endDate: string;
  pricePerPerson: number;
  seatsAvailable: number;
  capacity: number;
  isBookable: boolean;
  status: DepartureStatus;
}

export interface PackageDetail {
  id: number;
  slug: string;
  title: string;
  summary: string;
  description: string;
  destination: string;
  country: string;
  category: PackageCategory;
  durationDays: number;
  basePrice: number;
  imageUrl: string | null;
  highlights: string[];
  inclusions: string[];
  departures: Departure[];
}

export interface CatalogFilters {
  countries: string[];
  categories: PackageCategory[];
  maxPrice: number;
}

export interface PackageQuery {
  search?: string;
  category?: PackageCategory | '';
  country?: string;
  maxPrice?: number | null;
  sort?: string;
  page?: number;
  pageSize?: number;
}

// ---------- bookings ----------

export interface Traveller {
  fullName: string;
  nationality: string;
  passportNumber: string | null;
  dateOfBirth: string | null;
}

export interface Quote {
  unitPrice: number;
  seats: number;
  subtotal: number;
  discount: number;
  total: number;
  promoCode: string | null;
  promoMessage: string | null;
  seatsAvailable: number;
}

export interface CreateBookingRequest {
  departureId: number;
  travellers: Traveller[];
  contactEmail: string;
  contactPhone: string;
  promoCode: string | null;
  specialRequests: string | null;
}

export interface Payment {
  amount: number;
  status: PaymentStatus;
  transactionId: string;
  method: string;
  createdAt: string;
}

export interface Booking {
  id: string;
  reference: string;
  status: BookingStatus;
  package: {
    slug: string;
    title: string;
    destination: string;
    country: string;
    durationDays: number;
    category: PackageCategory;
  };
  departureDate: string;
  returnDate: string;
  seats: number;
  unitPrice: number;
  subtotal: number;
  discount: number;
  total: number;
  promoCode: string | null;
  refundAmount: number;
  customerName: string;
  contactEmail: string;
  contactPhone: string;
  specialRequests: string | null;
  travellers: Traveller[];
  payments: Payment[];
  createdAt: string;
  expiresAt: string;
  confirmedAt: string | null;
  cancelledAt: string | null;
  cancellationReason: string | null;
  canPay: boolean;
  canCancel: boolean;
}

export interface BookingListItem {
  id: string;
  reference: string;
  status: BookingStatus;
  packageTitle: string;
  packageSlug: string;
  destination: string;
  departureDate: string;
  seats: number;
  total: number;
  customerName: string;
  createdAt: string;
}

export interface CancellationQuote {
  amountPaid: number;
  refundPercent: number;
  refund: number;
  fee: number;
  daysBeforeDeparture: number;
  policy: { minDaysBefore: number; refundPercent: number }[];
}

// ---------- admin ----------

export interface Dashboard {
  revenueThisMonth: number;
  revenueLastMonth: number;
  confirmedBookingsThisMonth: number;
  pendingPayments: number;
  travellersNext30Days: number;
  averageOccupancy: number;
  totalCustomers: number;
  revenueByMonth: { year: number; month: number; revenue: number; bookings: number }[];
  topPackages: { id: number; title: string; destination: string; bookings: number; travellers: number; revenue: number }[];
  upcomingDepartures: {
    id: number;
    packageId: number;
    packageTitle: string;
    startDate: string;
    capacity: number;
    seatsBooked: number;
    occupancyPercent: number;
  }[];
  recentBookings: BookingListItem[];
}

export interface AdminPackageListItem {
  id: number;
  slug: string;
  title: string;
  destination: string;
  country: string;
  category: PackageCategory;
  durationDays: number;
  basePrice: number;
  isPublished: boolean;
  upcomingDepartures: number;
  seatsSold: number;
}

export interface AdminDeparture {
  id: number;
  startDate: string;
  capacity: number;
  seatsBooked: number;
  priceOverride: number | null;
  pricePerPerson: number;
  status: DepartureStatus;
  confirmedBookings: number;
  pendingBookings: number;
}

export interface AdminPackageDetail {
  id: number;
  slug: string;
  title: string;
  summary: string;
  description: string;
  destination: string;
  country: string;
  category: PackageCategory;
  durationDays: number;
  basePrice: number;
  imageUrl: string | null;
  highlights: string[];
  inclusions: string[];
  isPublished: boolean;
  departures: AdminDeparture[];
}

export interface UpsertPackageRequest {
  title: string;
  summary: string;
  description: string;
  destination: string;
  country: string;
  category: PackageCategory;
  durationDays: number;
  basePrice: number;
  imageUrl: string | null;
  highlights: string[];
  inclusions: string[];
}

export interface Manifest {
  departureId: number;
  packageTitle: string;
  startDate: string;
  capacity: number;
  seatsBooked: number;
  travellers: {
    bookingReference: string;
    fullName: string;
    nationality: string;
    passportNumber: string | null;
    dateOfBirth: string | null;
    contactPhone: string;
    specialRequests: string | null;
  }[];
}

export interface PromoCode {
  id: number;
  code: string;
  description: string;
  discountPercent: number;
  maxDiscount: number | null;
  validUntil: string;
  maxUses: number | null;
  usedCount: number;
  isActive: boolean;
}

export interface BookingEvent {
  reference: string;
  event: 'BookingConfirmed' | 'BookingCancelled';
  customerName: string;
  packageTitle: string;
  departureDate: string;
  seats: number;
  total: number;
}

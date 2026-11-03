import { Component, Pipe, PipeTransform, computed, input } from '@angular/core';
import { BookingStatus, PackageCategory } from '../core/models';

/** 1234.5 → "QAR 1,234.50" (drops .00 for whole amounts) */
@Pipe({ name: 'qar' })
export class QarPipe implements PipeTransform {
  transform(value: number | null | undefined): string {
    if (value === null || value === undefined) return '';
    const whole = Number.isInteger(value);
    return 'QAR ' + value.toLocaleString('en-US', { minimumFractionDigits: whole ? 0 : 2, maximumFractionDigits: 2 });
  }
}

export const CATEGORY_LABELS: Record<PackageCategory, string> = {
  City: 'City break',
  Desert: 'Desert',
  Beach: 'Beach & islands',
  Adventure: 'Adventure',
  Cultural: 'Culture & heritage',
  Honeymoon: 'Honeymoon',
};

export const STATUS_LABELS: Record<BookingStatus, string> = {
  PendingPayment: 'Awaiting payment',
  Confirmed: 'Confirmed',
  Cancelled: 'Cancelled',
  Expired: 'Expired',
};

/**
 * Package artwork. Without a photo, the destination name itself becomes the image,
 * set large in the display face on a colour field chosen by trip category.
 */
@Component({
  selector: 'app-cover',
  template: `
    <div class="cover" [class]="'cover cover--' + category().toLowerCase() + ' cover--' + size()">
      @if (imageUrl()) {
        <img [src]="imageUrl()" [alt]="destination()" loading="lazy" />
      } @else {
        <span class="cover__country">{{ country() }}</span>
        <span class="cover__name" aria-hidden="true">{{ destination() }}</span>
      }
    </div>
  `,
})
export class Cover {
  readonly destination = input.required<string>();
  readonly country = input<string>('');
  readonly category = input.required<PackageCategory>();
  readonly imageUrl = input<string | null>(null);
  readonly size = input<'card' | 'hero' | 'thumb'>('card');
}

@Component({
  selector: 'app-status',
  template: `<span [class]="'status status--' + status().toLowerCase()">{{ label() }}</span>`,
})
export class StatusChip {
  readonly status = input.required<BookingStatus>();
  protected readonly label = computed(() => STATUS_LABELS[this.status()]);
}

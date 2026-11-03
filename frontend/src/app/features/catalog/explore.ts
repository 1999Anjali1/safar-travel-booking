import { DatePipe, LowerCasePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CatalogApi, firstError } from '../../core/api';
import { CATEGORIES, PackageCard, PackageCategory, PagedResult } from '../../core/models';
import { CATEGORY_LABELS, Cover, QarPipe } from '../../shared/ui';

@Component({
  selector: 'app-explore',
  imports: [FormsModule, RouterLink, DatePipe, LowerCasePipe, QarPipe, Cover],
  templateUrl: './explore.html',
})
export class Explore {
  private readonly api = inject(CatalogApi);
  private readonly router = inject(Router);

  // Filters live in the URL (?category=Desert&sort=price-asc) so results can be shared and bookmarked
  // (a missing query parameter arrives as undefined, so each input falls back to a default)
  readonly search = input('', { transform: (v: string | undefined) => v ?? '' });
  readonly category = input('', { transform: (v: PackageCategory | '' | undefined) => v ?? '' });
  readonly country = input('', { transform: (v: string | undefined) => v ?? '' });
  readonly sort = input('popular', { transform: (v: string | undefined) => v || 'popular' });
  readonly page = input('1', { transform: (v: string | undefined) => v ?? '1' });

  protected readonly categories = CATEGORIES;
  protected readonly categoryLabels = CATEGORY_LABELS;
  protected readonly filters = toSignal(this.api.filters());

  protected readonly result = signal<PagedResult<PackageCard> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected searchText = '';

  protected readonly pageNumber = computed(() => Math.max(1, Number(this.page()) || 1));

  constructor() {
    effect(() => {
      const query = {
        search: this.search(),
        category: this.category(),
        country: this.country(),
        sort: this.sort(),
        page: this.pageNumber(),
        pageSize: 9,
      };
      this.searchText = query.search ?? '';
      this.loading.set(true);
      this.api.search(query).subscribe({
        next: (r) => {
          this.result.set(r);
          this.error.set(null);
          this.loading.set(false);
        },
        error: (e) => {
          this.error.set(firstError(e));
          this.loading.set(false);
        },
      });
    });
  }

  update(changes: Record<string, string | number | null>): void {
    this.router.navigate([], {
      queryParams: { page: null, ...changes },
      queryParamsHandling: 'merge',
    });
  }

  submitSearch(): void {
    this.update({ search: this.searchText.trim() || null });
  }

  clearFilters(): void {
    this.router.navigate([]);
  }
}

import { Injectable, computed, inject, signal } from '@angular/core';
import { httpResource } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { AdminPhotosApi, type PhotoDeleteResult, type PhotoListItem } from '@coffee-tracker/data';

export type PhotoFilter = 'all' | 'unused';

/**
 * Page-scoped store (provided by the component, not root) for the admin photo-cleanup
 * screen. The list is an `httpResource`; the filter and the selected-paths set are
 * plain signals composed into the derived `visible`/count signals.
 */
@Injectable()
export class PhotoCleanupStore {
  private readonly api = inject(AdminPhotosApi);
  private readonly resource = httpResource<PhotoListItem[]>(() => '/api/admin/photos', {
    defaultValue: [],
  });

  // httpResource.value throws while the resource is errored — guard reads so the
  // template's count/visible derivations stay safe on the error path (see CoffeesStore).
  readonly photos = computed(() => (this.resource.error() ? [] : this.resource.value()));
  readonly loading = this.resource.isLoading;
  readonly error = computed(() => (this.resource.error() ? 'Could not load stored photos.' : null));

  readonly filter = signal<PhotoFilter>('all');
  /** Selected paths. Only unused photos are ever added (used ones aren't selectable). */
  private readonly _selection = signal<ReadonlySet<string>>(new Set());
  readonly selection = this._selection.asReadonly();

  readonly storedCount = computed(() => this.photos().length);
  readonly unusedCount = computed(() => this.photos().filter((p) => !p.used).length);
  readonly selectedCount = computed(() => this._selection().size);

  readonly visible = computed(() =>
    this.filter() === 'unused' ? this.photos().filter((p) => !p.used) : this.photos(),
  );

  isSelected(path: string): boolean {
    return this._selection().has(path);
  }

  toggle(path: string): void {
    this._selection.update((set) => {
      const next = new Set(set);
      if (next.has(path)) {
        next.delete(path);
      } else {
        next.add(path);
      }
      return next;
    });
  }

  selectAllUnused(): void {
    this._selection.set(new Set(this.photos().filter((p) => !p.used).map((p) => p.path)));
  }

  clearSelection(): void {
    this._selection.set(new Set());
  }

  setFilter(value: PhotoFilter): void {
    this.filter.set(value);
  }

  /** Deletes the current selection, clears it, and refetches the list. */
  async deleteSelected(): Promise<PhotoDeleteResult> {
    const result = await firstValueFrom(this.api.delete([...this._selection()]));
    this.clearSelection();
    this.resource.reload();
    return result;
  }
}

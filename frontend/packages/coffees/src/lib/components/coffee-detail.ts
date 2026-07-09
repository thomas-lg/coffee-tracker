import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Button, Rating, Skeleton, TagChip, ToastService } from '@coffee-tracker/ui';
import { formatDate, formatPrice, formatRating } from '@coffee-tracker/util';
import { AuthStore } from '@coffee-tracker/auth';
import { CoffeesApi, FlavorTagsApi, ReviewsApi } from '@coffee-tracker/data';
import { roastGradient } from '../utils/coffee-visual';
import { CoffeesStore } from '../services/coffees.store';
import { BeanScene } from './bean-scene';

@Component({
  selector: 'ct-coffee-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Button, Rating, TagChip, Skeleton, BeanScene],
  templateUrl: './coffee-detail.html',
})
export class CoffeeDetail {
  private readonly coffeesApi = inject(CoffeesApi);
  private readonly reviewsApi = inject(ReviewsApi);
  private readonly tagsApi = inject(FlavorTagsApi);
  private readonly store = inject(CoffeesStore);
  private readonly auth = inject(AuthStore);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly id = input.required<string>();
  protected readonly coffeeId = computed(() => Number(this.id()));

  // Reactive reads through the data-layer services — refetch when the route id changes.
  private readonly coffeeRes = rxResource({
    params: () => this.coffeeId(),
    stream: ({ params }) => this.coffeesApi.get(params),
  });
  private readonly reviewsRes = rxResource({
    params: () => this.coffeeId(),
    stream: ({ params }) => this.reviewsApi.listForCoffee(params),
    defaultValue: [],
  });
  private readonly tagsRes = rxResource({
    stream: () => this.tagsApi.list(),
    defaultValue: [],
  });

  // rxResource.value() THROWS while the resource is in an error state — guard the
  // reads (same pattern as CoffeesStore) so a 404/failed load renders the
  // "Coffee not found" branch instead of blowing up mid-render.
  protected readonly coffee = computed(() =>
    this.coffeeRes.error() ? undefined : this.coffeeRes.value(),
  );
  protected readonly reviews = computed(() =>
    this.reviewsRes.error() ? [] : this.reviewsRes.value(),
  );
  protected readonly tags = computed(() => (this.tagsRes.error() ? [] : this.tagsRes.value()));
  protected readonly loading = computed(() => this.coffeeRes.isLoading() && !this.coffee());

  protected readonly roastGradient = roastGradient;
  protected readonly formatRating = formatRating;
  protected readonly formatPrice = formatPrice;
  protected readonly formatDate = formatDate;

  private readonly myId = computed(() => this.auth.session()?.userId ?? null);
  protected readonly myReviews = computed(() => {
    const me = this.myId();
    return me ? this.reviews().filter((r) => r.userId === me) : [];
  });
  protected readonly otherReviews = computed(() => {
    const me = this.myId();
    return this.reviews().filter((r) => r.userId !== me);
  });

  protected readonly specs = computed<[string, string][]>(() => {
    const c = this.coffee();
    if (!c) return [];
    const rows: [string, string][] = [
      ['Origin', c.origin],
      ['Roaster', c.roaster],
      ['Roast', c.roastLevel],
      ['Price', this.formatPrice(c.price)],
      ['Bought', this.formatDate(c.dateBought)],
    ];
    if (c.shopName) rows.push(['Shop', c.shopName]);
    return rows;
  });

  // "Rate today"
  protected readonly newRating = signal(0);
  protected readonly newStage = signal('');
  protected readonly newNotes = signal('');
  protected readonly selectedTags = signal<ReadonlySet<number>>(new Set());
  protected readonly saving = signal(false);

  protected isTagOn(id: number): boolean {
    return this.selectedTags().has(id);
  }

  protected toggleTag(id: number): void {
    this.selectedTags.update((s) => {
      const next = new Set(s);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  protected async rate(): Promise<void> {
    if (this.newRating() < 1 || this.saving()) return;
    this.saving.set(true);
    try {
      await firstValueFrom(
        this.reviewsApi.create(this.coffeeId(), {
          rating: this.newRating(),
          stage: this.newStage() || null,
          tastingNotes: this.newNotes() || null,
          tagIds: [...this.selectedTags()],
        }),
      );
      this.toast.show('Rating saved for today.', 'success');
      this.newRating.set(0);
      this.newStage.set('');
      this.newNotes.set('');
      this.selectedTags.set(new Set());
      this.reviewsRes.reload();
      this.coffeeRes.reload(); // refresh this coffee's average
      this.store.reload(); // keep the grid's cached average in sync (root singleton; only refetches on reload)
    } catch {
      this.toast.show('Could not save your rating.', 'error');
    } finally {
      this.saving.set(false);
    }
  }

  // In-page armed-confirm for delete (no native confirm(): unstyled, untestable,
  // and suppressible in installed PWAs). Mirrors the admin photo-cleanup pattern.
  protected readonly confirmingDelete = signal(false);
  protected readonly deleting = signal(false);
  private readonly cancelDeleteBtn = viewChild<ElementRef<HTMLButtonElement>>('cancelDeleteBtn');

  constructor() {
    // Arming the confirm removes the Delete button (focus would drop to <body>);
    // move it to Cancel once the confirm controls exist in the DOM.
    effect(() => {
      if (this.confirmingDelete()) this.cancelDeleteBtn()?.nativeElement.focus();
    });
  }

  protected armDelete(): void {
    this.confirmingDelete.set(true);
  }

  protected cancelDelete(): void {
    this.confirmingDelete.set(false);
  }

  protected async confirmDelete(): Promise<void> {
    const c = this.coffee();
    if (!c || this.deleting()) return;
    this.deleting.set(true);
    try {
      await firstValueFrom(this.coffeesApi.delete(c.id));
      this.store.reload(); // a delete changes the catalog list
      this.toast.show('Coffee deleted.', 'success');
      await this.router.navigate(['/coffees']);
    } catch {
      this.toast.show('Could not delete it (you may not have permission).', 'error');
      this.deleting.set(false);
      this.confirmingDelete.set(false);
    }
  }
}

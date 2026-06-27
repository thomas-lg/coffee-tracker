import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { Button, Rating, Skeleton, TagChip, ToastService } from '@coffee-tracker/ui';
import { formatDate, formatPrice, formatRating } from '@coffee-tracker/util';
import { AuthStore } from '@coffee-tracker/auth';
import {
  CoffeesApi,
  FlavorTagsApi,
  ReviewsApi,
  type Coffee,
  type FlavorTag,
  type Review,
} from '@coffee-tracker/data';
import { roastGradient } from './coffee-visual';
import { BeanScene } from './bean-scene';

@Component({
  selector: 'ct-coffee-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Button, Rating, TagChip, Skeleton, BeanScene],
  templateUrl: './coffee-detail.html',
})
export class CoffeeDetail {
  private readonly api = inject(CoffeesApi);
  private readonly reviewsApi = inject(ReviewsApi);
  private readonly tagsApi = inject(FlavorTagsApi);
  private readonly auth = inject(AuthStore);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  readonly id = input.required<string>();
  protected readonly coffeeId = computed(() => Number(this.id()));

  protected readonly coffee = signal<Coffee | null>(null);
  protected readonly reviews = signal<Review[]>([]);
  protected readonly tags = signal<FlavorTag[]>([]);
  protected readonly loading = signal(true);

  protected readonly roastGradient = roastGradient;
  protected readonly formatRating = formatRating;
  protected readonly formatPrice = formatPrice;
  protected readonly formatDate = formatDate;

  private readonly myId = computed(() => this.auth.session()?.userId ?? null);
  protected readonly myReviews = computed(() => this.reviews().filter((r) => r.userId === this.myId()));
  protected readonly otherReviews = computed(() => this.reviews().filter((r) => r.userId !== this.myId()));

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

  constructor() {
    effect(() => void this.load(this.coffeeId()));
    firstValueFrom(this.tagsApi.list())
      .then((t) => this.tags.set(t))
      .catch(() => undefined);
  }

  private async load(id: number): Promise<void> {
    this.loading.set(true);
    try {
      const [coffee, reviews] = await Promise.all([
        firstValueFrom(this.api.get(id)),
        firstValueFrom(this.reviewsApi.listForCoffee(id)),
      ]);
      this.coffee.set(coffee);
      this.reviews.set(reviews);
    } catch {
      this.toast.show('Could not load that coffee.', 'error');
    } finally {
      this.loading.set(false);
    }
  }

  protected isTagOn(id: number): boolean {
    return this.selectedTags().has(id);
  }

  protected toggleTag(id: number): void {
    this.selectedTags.update((s) => {
      const next = new Set(s);
      next.has(id) ? next.delete(id) : next.add(id);
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
      await this.load(this.coffeeId());
    } catch {
      this.toast.show('Could not save your rating.', 'error');
    } finally {
      this.saving.set(false);
    }
  }

  protected async remove(): Promise<void> {
    const c = this.coffee();
    if (!c || !confirm(`Delete “${c.name}”? This removes its reviews too.`)) return;
    try {
      await firstValueFrom(this.api.delete(c.id));
      this.toast.show('Coffee deleted.', 'success');
      await this.router.navigate(['/coffees']);
    } catch {
      this.toast.show('Could not delete it (you may not have permission).', 'error');
    }
  }
}

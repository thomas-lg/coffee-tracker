import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button, ImageLightbox, Rating, Skeleton, TagChip } from '@coffee-tracker/ui';
import { formatDate, formatPrice, formatRating } from '@coffee-tracker/util';
import { roastGradient } from '@coffees/utils/coffee-visual';
import { CoffeeDetailStore } from './coffee-detail.store';
import { BeanScene } from '@coffees/components/bean-scene/bean-scene';

@Component({
  selector: 'ct-coffee-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, Button, Rating, TagChip, Skeleton, BeanScene, ImageLightbox],
  providers: [CoffeeDetailStore],
  templateUrl: './coffee-detail.html',
})
export class CoffeeDetail {
  protected readonly store = inject(CoffeeDetailStore);

  readonly id = input.required<string>();

  protected readonly roastGradient = roastGradient;
  protected readonly formatRating = formatRating;
  protected readonly formatPrice = formatPrice;
  protected readonly formatDate = formatDate;

  /** The spec table's rows, in display order, presentation, so it stays here. */
  protected readonly specs = computed<[string, string][]>(() => {
    const c = this.store.coffee();
    if (!c) return [];
    const rows: [string, string][] = [
      ['Origin', c.origin],
      ['Roaster', c.roaster],
      ['Roast', c.roastLevel],
      ['Price', formatPrice(c.price)],
      ['Bought', formatDate(c.dateBought)],
    ];
    if (c.shopName) rows.push(['Shop', c.shopName]);
    return rows;
  });

  private readonly cancelDeleteBtn = viewChild<ElementRef<HTMLButtonElement>>('cancelDeleteBtn');
  private readonly armDeleteBtn = viewChild<ElementRef<HTMLButtonElement>>('armDeleteBtn');

  /** The route parameter arrives as a string; the store reads by number. */
  private readonly coffeeId = computed(() => Number(this.id()));

  private readonly injector = inject(Injector);

  constructor() {
    // The signal, not its value: the router reuses this component when only the
    // parameter changes, so the store has to keep hearing about it.
    this.store.setCoffeeId(this.coffeeId);
  }

  /**
   * Arming and disarming each remove the element that currently has focus, so without
   * this it drops to <body> and a keyboard user loses their place mid-flow. Moving it
   * both ways is what makes the confirm dismissable as well as reachable. autofocus is
   * banned by the template a11y lint, so it is moved by hand.
   *
   * afterNextRender, not an effect on `confirmingDelete`: an effect runs before the
   * template that creates the button it wants to focus, so the viewChild is still empty
   * when it reads it and nothing moves at all.
   */
  protected armDelete(): void {
    this.store.armDelete();
    afterNextRender(() => this.cancelDeleteBtn()?.nativeElement.focus(), {
      injector: this.injector,
    });
  }

  protected cancelDelete(): void {
    this.store.cancelDelete();
    afterNextRender(() => this.armDeleteBtn()?.nativeElement.focus(), {
      injector: this.injector,
    });
  }
}

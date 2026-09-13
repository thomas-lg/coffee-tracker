import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  input,
  viewChild,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { Button, ImageLightbox, Rating, Skeleton, TagChip } from '@coffee-tracker/ui';
import { formatDate, formatPrice, formatRating } from '@coffee-tracker/util';
import { roastGradient } from '../utils/coffee-visual';
import { CoffeeDetailStore } from '../services/coffee-detail.store';
import { BeanScene } from './bean-scene';

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

  /** The spec table's rows, in display order — presentation, so it stays here. */
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

  constructor() {
    // The route parameter arrives as a string; the store reads by number.
    effect(() => this.store.setCoffeeId(Number(this.id())));

    // Arming and disarming each remove the element that currently has focus, so without
    // this focus drops to <body> and a keyboard user loses their place mid-flow. Moving
    // it in both directions is what makes the confirm dismissable as well as reachable.
    // autofocus is banned by the template a11y lint, so it is moved by hand.
    //
    // Only on a transition: this effect also runs on first render, and focusing the
    // Delete button merely because the page loaded would yank focus out of wherever the
    // reader actually is.
    let wasConfirming: boolean | undefined;
    effect(() => {
      const confirming = this.store.confirmingDelete();
      if (wasConfirming !== undefined && wasConfirming !== confirming) {
        const button = confirming ? this.cancelDeleteBtn() : this.armDeleteBtn();
        button?.nativeElement.focus();
      }
      wasConfirming = confirming;
    });
  }
}
